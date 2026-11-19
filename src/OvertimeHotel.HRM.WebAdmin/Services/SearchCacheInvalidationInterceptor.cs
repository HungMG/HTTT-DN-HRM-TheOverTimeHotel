using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed class SearchCacheSaveInterceptor(SearchListCache cache, SearchCacheTransactionInterceptor transactions) : SaveChangesInterceptor
{
    private void Invalidate(SaveChangesCompletedEventData eventData, int result)
    {
        if (result <= 0) return;
        transactions.MarkDirty(eventData.Context?.Database.CurrentTransaction?.GetDbTransaction());

        if (eventData.Context == null)
        {
            cache.Invalidate();
            return;
        }

        var entities = eventData.Context.ChangeTracker.Entries()
            .Select(e => e.Metadata.ClrType.Name)
            .ToHashSet();

        bool matched = false;
        if (entities.Contains("NhanVien") || entities.Contains("HopDong")) { cache.Invalidate("employees"); matched = true; }
        if (entities.Contains("HopDong") || entities.Contains("PhuLucLuong")) { cache.Invalidate("contracts"); matched = true; }
        if (entities.Contains("PhanCa") || entities.Contains("YeuCauDoiCa") || entities.Contains("DinhBienCa") || entities.Contains("DinhBienNgay")) { cache.Invalidate("phanca"); matched = true; }
        if (entities.Contains("ChamCong") || entities.Contains("GiaiTrinhChamCong")) { cache.Invalidate("attendance"); cache.Invalidate("giai-trinh"); cache.Invalidate("attendance-explanations"); matched = true; }
        if (entities.Contains("DonTu") || entities.Contains("LoaiDon")) { cache.Invalidate("leave-requests"); matched = true; }
        if (entities.Contains("TaiKhoan") || entities.Contains("VaiTro") || entities.Contains("YeuCauTaiKhoan")) { cache.Invalidate("accounts"); cache.Invalidate("account-requests"); matched = true; }
        if (entities.Contains("KyLuong") || entities.Contains("BangLuong") || entities.Contains("PhieuLuong") || entities.Contains("ChiTietPhieuLuong") || entities.Contains("ChiTietBangLuong")) { cache.Invalidate("payroll"); cache.Invalidate("payroll-config"); matched = true; }
        if (entities.Contains("PhongBan")) { cache.Invalidate("departments"); cache.Invalidate("employees"); cache.Invalidate("contracts"); matched = true; }
        if (entities.Contains("ChucVu")) { cache.Invalidate("positions"); cache.Invalidate("employees"); cache.Invalidate("contracts"); matched = true; }
        if (entities.Contains("CaLamViec")) { cache.Invalidate("shifts"); cache.Invalidate("phanca"); cache.Invalidate("attendance"); matched = true; }

        if (!matched)
        {
            cache.Invalidate();
        }
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Invalidate(eventData, result);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Invalidate(eventData, result);
        return ValueTask.FromResult(result);
    }
}

// ExecuteUpdate/Delete and raw SQL bypass SaveChanges; invalidate their list snapshots too.
public sealed class SearchCacheCommandInterceptor(SearchListCache cache, SearchCacheTransactionInterceptor transactions) : DbCommandInterceptor
{
    private static readonly Regex WriteSql = new(@"\b(INSERT|UPDATE|DELETE|TRUNCATE|MERGE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private void Invalidate(DbCommand command)
    {
        if (WriteSql.IsMatch(command.CommandText))
        {
            transactions.MarkDirty(command.Transaction);
            var text = command.CommandText;
            bool matched = false;
            if (text.Contains("nhan_vien", StringComparison.OrdinalIgnoreCase) || text.Contains("hop_dong", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("employees"); matched = true; }
            if (text.Contains("hop_dong", StringComparison.OrdinalIgnoreCase) || text.Contains("phu_luc_luong", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("contracts"); matched = true; }
            if (text.Contains("phan_ca", StringComparison.OrdinalIgnoreCase) || text.Contains("yeu_cau_doi_ca", StringComparison.OrdinalIgnoreCase) || text.Contains("dinh_bien", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("phanca"); matched = true; }
            if (text.Contains("cham_cong", StringComparison.OrdinalIgnoreCase) || text.Contains("giai_trinh", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("attendance"); cache.Invalidate("giai-trinh"); cache.Invalidate("attendance-explanations"); matched = true; }
            if (text.Contains("don_tu", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("leave-requests"); matched = true; }
            if (text.Contains("tai_khoan", StringComparison.OrdinalIgnoreCase) || text.Contains("vai_tro", StringComparison.OrdinalIgnoreCase) || text.Contains("yeu_cau_tai_khoan", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("accounts"); cache.Invalidate("account-requests"); matched = true; }
            if (text.Contains("ky_luong", StringComparison.OrdinalIgnoreCase) || text.Contains("bang_luong", StringComparison.OrdinalIgnoreCase) || text.Contains("phieu_luong", StringComparison.OrdinalIgnoreCase) || text.Contains("chi_tiet_phieu_luong", StringComparison.OrdinalIgnoreCase)) { cache.Invalidate("payroll"); cache.Invalidate("payroll-config"); matched = true; }

            if (!matched)
            {
                cache.Invalidate();
            }
        }
    }
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Invalidate(command); return result; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Invalidate(command); return result; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Invalidate(command); return result; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { Invalidate(command); return ValueTask.FromResult(result); }
}

// A concurrent read may have seen the old state while an explicit transaction was open.
public sealed class SearchCacheTransactionInterceptor(SearchListCache cache) : DbTransactionInterceptor
{
    private readonly ConditionalWeakTable<DbTransaction, object> _dirty = new();
    public void MarkDirty(DbTransaction? transaction)
    {
        if (transaction != null) _dirty.GetValue(transaction, _ => new object());
    }
    private void Complete(DbTransaction transaction)
    {
        if (_dirty.Remove(transaction)) cache.Invalidate();
    }
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Complete(transaction); return Task.CompletedTask; }
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Complete(transaction);
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    { Complete(transaction); return Task.CompletedTask; }
}
