namespace OvertimeHotel.HRM.Data.Supabase;

public static class SupabaseClientFactory
{
    public static global::Supabase.Client CreateClient(string url, string key)
    {
        var options = new global::Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true
        };

        return new global::Supabase.Client(url, key, options);
    }
}
