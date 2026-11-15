using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace OvertimeHotel.HRM.WebAdmin.Services;

/// <summary>Read-only list snapshots, shared across requests for at most 15 minutes.</summary>
public sealed class SearchListCache(IMemoryCache memory, TimeProvider? clock = null) : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        IncludeFields = true
    };
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    // Striped locks: 64 semaphores eliminate cold-load contention across different keys while protecting against thundering herd.
    private const int StripeCount = 64;
    private readonly SemaphoreSlim[] _loadLocks = Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly object _generationLock = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _prefixGenerations = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _globalGeneration = new();
    private sealed record Snapshot(byte[] Data, DateTimeOffset ExpiresAt);

    private static string ExtractPrefix(string key)
    {
        var idx = key.IndexOf(':');
        return idx > 0 ? key[..idx] : key;
    }

    public async Task<T> GetAsync<T>(string key, Func<Task<T>> load)
    {
        var cacheKey = (typeof(SearchListCache), typeof(T), key);
        if (!TryRead(cacheKey, out var snapshot))
        {
            var stripeIndex = Math.Abs(cacheKey.GetHashCode()) % StripeCount;
            var loadLock = _loadLocks[stripeIndex];
            await loadLock.WaitAsync();
            try
            {
                if (!TryRead(cacheKey, out snapshot))
                {
                    var prefix = ExtractPrefix(key);
                    CancellationToken prefixToken;
                    CancellationToken globalToken;
                    lock (_generationLock)
                    {
                        prefixToken = _prefixGenerations.GetOrAdd(prefix, _ => new CancellationTokenSource()).Token;
                        globalToken = _globalGeneration.Token;
                    }

                    var data = await load();
                    snapshot = new Snapshot(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions), _clock.GetUtcNow() + Lifetime);
                    lock (_generationLock)
                    {
                        // A write that finished during this load must not be overwritten by stale data.
                        if (!prefixToken.IsCancellationRequested && !globalToken.IsCancellationRequested)
                        {
                            memory.Set(cacheKey, snapshot, new MemoryCacheEntryOptions()
                                .SetAbsoluteExpiration(Lifetime)
                                .AddExpirationToken(new CancellationChangeToken(prefixToken))
                                .AddExpirationToken(new CancellationChangeToken(globalToken)));
                        }
                    }
                }
            }
            finally { loadLock.Release(); }
        }
        // Copies are detached from EF and cannot be modified by a different request.
        return JsonSerializer.Deserialize<T>(snapshot!.Data, JsonOptions)!;
    }

    private bool TryRead(object key, out Snapshot? snapshot) =>
        memory.TryGetValue(key, out snapshot) && snapshot != null && snapshot.ExpiresAt > _clock.GetUtcNow();

    public void Invalidate(string? prefix = null)
    {
        lock (_generationLock)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                if (_prefixGenerations.TryRemove(prefix, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
            else
            {
                var prevGlobal = _globalGeneration;
                _globalGeneration = new CancellationTokenSource();
                prevGlobal.Cancel();
                prevGlobal.Dispose();

                foreach (var (_, cts) in _prefixGenerations)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _prefixGenerations.Clear();
            }
        }
    }

    public void Dispose()
    {
        lock (_generationLock)
        {
            _globalGeneration.Cancel();
            _globalGeneration.Dispose();
            foreach (var (_, cts) in _prefixGenerations)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _prefixGenerations.Clear();
        }
        foreach (var l in _loadLocks)
        {
            l.Dispose();
        }
    }
}

public static class SearchListCacheExtensions
{
    public static Task<T> SearchDataAsync<T>(this Controller controller, string key, Func<Task<T>> load) =>
        controller.HttpContext?.RequestServices?.GetService<SearchListCache>() is { } cache
            ? cache.GetAsync(key, load)
            : load();
}
