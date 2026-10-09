namespace OvertimeHotel.HRM.MobileApp.Models;

public record DepartmentOption(int? MaPhongBan, string TenPhongBan)
{
    public string PathData => TenPhongBan switch
    {
        string s when s.Contains("Tất cả", StringComparison.OrdinalIgnoreCase)
            => "M3 21h18 M3 7v14 M21 7v14 M6 10h2 M6 14h2 M6 18h2 M11 10h2 M11 14h2 M11 18h2 M16 10h2 M16 14h2 M16 18h2 M3 7l9-4 9 4",

        string s when s.Contains("Tiền Sảnh", StringComparison.OrdinalIgnoreCase) || s.Contains("Lễ tân", StringComparison.OrdinalIgnoreCase) || s.Contains("FO", StringComparison.OrdinalIgnoreCase)
            => "M2 18h20 M4 18v-1a8 8 0 0 1 16 0v1 M12 4v5 M10 4h4",

        string s when s.Contains("Buồng", StringComparison.OrdinalIgnoreCase) || s.Contains("HK", StringComparison.OrdinalIgnoreCase)
            => "M2 4v16 M2 8h20 M22 4v16 M2 17h20 M6 8v3 M18 8v3",

        string s when s.Contains("Ẩm Thực", StringComparison.OrdinalIgnoreCase) || s.Contains("F&B", StringComparison.OrdinalIgnoreCase) || s.Contains("Bếp", StringComparison.OrdinalIgnoreCase)
            => "M18 2v20 M14 2v4a4 4 0 0 0 8 0V2 M2 2v7a4 4 0 0 0 4 4v9 M6 2v7",

        string s when s.Contains("Nhân Sự", StringComparison.OrdinalIgnoreCase) || s.Contains("HR", StringComparison.OrdinalIgnoreCase)
            => "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M22 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75",

        string s when s.Contains("Kế Toán", StringComparison.OrdinalIgnoreCase) || s.Contains("Tài Chính", StringComparison.OrdinalIgnoreCase)
            => "M4 4h16v16H4z M4 9h16 M9 4v16 M15 13h2 M15 17h2",

        string s when s.Contains("CNTT", StringComparison.OrdinalIgnoreCase) || s.Contains("IT", StringComparison.OrdinalIgnoreCase) || s.Contains("Kỹ Thuật", StringComparison.OrdinalIgnoreCase)
            => "M4 4h16v12H4z M8 20h8 M12 16v4",

        _ => "M3 21h18 M5 21V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16 M9 7h2 M13 7h2 M9 11h2 M13 11h2 M9 15h2 M13 15h2"
    };

    public string Icon => TenPhongBan switch
    {
        string s when s.Contains("Tất cả", StringComparison.OrdinalIgnoreCase) => "🏨",
        string s when s.Contains("Tiền Sảnh", StringComparison.OrdinalIgnoreCase) || s.Contains("Lễ tân", StringComparison.OrdinalIgnoreCase) || s.Contains("FO", StringComparison.OrdinalIgnoreCase) => "🛎️",
        string s when s.Contains("Buồng", StringComparison.OrdinalIgnoreCase) || s.Contains("HK", StringComparison.OrdinalIgnoreCase) => "🧹",
        string s when s.Contains("Ẩm Thực", StringComparison.OrdinalIgnoreCase) || s.Contains("F&B", StringComparison.OrdinalIgnoreCase) || s.Contains("Bếp", StringComparison.OrdinalIgnoreCase) => "🍽️",
        string s when s.Contains("Nhân Sự", StringComparison.OrdinalIgnoreCase) || s.Contains("HR", StringComparison.OrdinalIgnoreCase) => "💼",
        string s when s.Contains("Kế Toán", StringComparison.OrdinalIgnoreCase) || s.Contains("Tài Chính", StringComparison.OrdinalIgnoreCase) => "📊",
        string s when s.Contains("CNTT", StringComparison.OrdinalIgnoreCase) || s.Contains("IT", StringComparison.OrdinalIgnoreCase) || s.Contains("Kỹ Thuật", StringComparison.OrdinalIgnoreCase) => "💻",
        string s when s.Contains("Giám Đốc", StringComparison.OrdinalIgnoreCase) || s.Contains("Ban Quản Lý", StringComparison.OrdinalIgnoreCase) => "🏛️",
        _ => "🏢"
    };
}
