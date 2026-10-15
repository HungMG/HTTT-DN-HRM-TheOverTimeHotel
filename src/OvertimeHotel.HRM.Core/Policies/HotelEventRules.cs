using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public static class HotelEventRules
{
    public static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>
    {
        ["LEN_KE_HOACH"] = "Lên kế hoạch", ["DANG_DIEN_RA"] = "Đang diễn ra",
        ["CHO_NGHIEM_THU"] = "Chờ nghiệm thu", ["HOAN_THANH"] = "Hoàn thành", ["HUY"] = "Đã hủy"
    };
    public static readonly IReadOnlyDictionary<string, string> TypeLabels = new Dictionary<string, string>
    {
        ["TIEC_CUOI"] = "Tiệc cưới", ["HOI_NGHI_MICE"] = "Hội nghị MICE", ["GALA_DINNER"] = "Gala Dinner",
        ["FESTIVAL_AM_THUC"] = "Festival ẩm thực", ["DOAN_VIP"] = "Đoàn VIP"
    };
    public static readonly IReadOnlyDictionary<string, string> DepartmentLabels = new Dictionary<string, string>
    {
        ["TIEN_SANH"] = "Tiền sảnh", ["AM_THUC"] = "Ẩm thực (F&B)", ["BEP"] = "Bếp",
        ["BUONG_PHONG"] = "Buồng phòng", ["KY_THUAT"] = "Kỹ thuật", ["AN_NINH"] = "An ninh",
        ["IT"] = "IT"
    };
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredDepartmentsByType = new Dictionary<string, IReadOnlyList<string>>
    {
        ["TIEC_CUOI"] = ["TIEN_SANH", "AM_THUC", "BEP", "BUONG_PHONG", "KY_THUAT", "AN_NINH"],
        ["HOI_NGHI_MICE"] = ["TIEN_SANH", "AM_THUC", "BEP", "KY_THUAT", "IT", "AN_NINH"],
        ["GALA_DINNER"] = ["TIEN_SANH", "AM_THUC", "BEP", "KY_THUAT", "AN_NINH"],
        ["FESTIVAL_AM_THUC"] = ["AM_THUC", "BEP", "BUONG_PHONG", "KY_THUAT", "AN_NINH"],
        ["DOAN_VIP"] = ["TIEN_SANH", "BUONG_PHONG", "AM_THUC", "BEP", "AN_NINH"]
    };
    public static readonly IReadOnlyDictionary<string, string> RatingLabels = new Dictionary<string, string>
    {
        ["CHUA_DANH_GIA"] = "Chưa đánh giá", ["HOAN_THANH"] = "Hoàn thành",
        ["XUAT_SAC"] = "Xuất sắc", ["KHONG_THAM_GIA"] = "Không tham gia"
    };

    public static string? ResolveDepartmentCode(int? maPhongBan, string? tenPhongBan = null)
    {
        if (maPhongBan == 2) return "TIEN_SANH";
        if (maPhongBan == 4) return "AM_THUC";
        if (maPhongBan == 5) return "BEP";
        if (maPhongBan == 3) return "BUONG_PHONG";
        if (maPhongBan == 6) return "KY_THUAT";
        if (maPhongBan == 7) return "AN_NINH";
        if (maPhongBan == 9) return "IT";

        if (!string.IsNullOrWhiteSpace(tenPhongBan))
        {
            var name = tenPhongBan.ToLowerInvariant();
            if (name.Contains("tiền sảnh") || name.Contains("tien_sanh") || name.Contains("tiensanh") || name.Contains("front office")) return "TIEN_SANH";
            if (name.Contains("ẩm thực") || name.Contains("am_thuc") || name.Contains("amthuc") || name.Contains("f&b")) return "AM_THUC";
            if (name.Contains("bếp") || name.Contains("bep") || name.Contains("kitchen") || name.Contains("culinary")) return "BEP";
            if (name.Contains("buồng phòng") || name.Contains("buong_phong") || name.Contains("buongphong") || name.Contains("housekeeping")) return "BUONG_PHONG";
            if (name.Contains("kỹ thuật") || name.Contains("ky_thuat") || name.Contains("kythuat") || name.Contains("engineering")) return "KY_THUAT";
            if (name.Contains("an ninh") || name.Contains("an_ninh") || name.Contains("anninh") || name.Contains("security")) return "AN_NINH";
            if (name.Contains("công nghệ thông tin") || name.Contains("information technology") || name.Contains("-it") || name.Contains(" it") || name == "it" || name.EndsWith("it")) return "IT";
        }
        return null;
    }

    public static string? ResolveDepartmentCode(NhanVien? employee) =>
        employee == null ? null : ResolveDepartmentCode(employee.MaPhongBan, employee.PhongBan?.TenPhongBan);

    public static IReadOnlyList<string> GetRequiredDepartments(string eventType) =>
        RequiredDepartmentsByType.GetValueOrDefault(eventType, []);

    public static EventStaffingSummary GetStaffingSummary(string eventType, IEnumerable<PhanCongDuAn> assignments)
    {
        var requiredCodes = GetRequiredDepartments(eventType);
        var assignmentList = assignments.ToList();
        var list = new List<EventDepartmentStatus>();
        var missing = new List<string>();
        int satisfied = 0;

        foreach (var code in requiredCodes)
        {
            var name = DepartmentLabels.GetValueOrDefault(code, code);
            var deptAssignments = assignmentList.Where(p => ResolveDepartmentCode(p.NhanVien) == code).ToList();
            var count = deptAssignments.Count;
            var ok = count >= 1;
            if (ok) satisfied++;
            else missing.Add(name);

            list.Add(new EventDepartmentStatus(code, name, count, ok, deptAssignments));
        }

        var total = requiredCodes.Count;
        var isFull = total > 0 && satisfied == total;
        var summary = isFull
            ? $"Đã đủ nhân sự {total}/{total} phòng ban."
            : (missing.Count == 0 && total == 0)
                ? "Sự kiện không yêu cầu phòng ban bắt buộc."
                : $"Đã đủ nhân sự {satisfied}/{total} phòng ban — còn thiếu {string.Join(", ", missing)}.";

        return new EventStaffingSummary(total, satisfied, isFull, missing, list, summary);
    }
    public static DateTimeOffset ToUtc(DateTime local) => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).ToUniversalTime();
    public static DateTime Local(DateTimeOffset instant) => instant.ToOffset(TimeSpan.FromHours(7)).DateTime;
    public static bool CanCreateOn(DateOnly date, DateTimeOffset now) => date >= DateOnly.FromDateTime(Local(now));
    public static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset otherStart, DateTimeOffset otherEnd) =>
        start < end && otherStart < otherEnd && start < otherEnd && otherStart < end;
    public static bool ContractCovers(NhanVien employee, DateTimeOffset start, DateTimeOffset end)
    {
        if (start >= end || employee.TrangThai != "DANG_LAM") return false;
        var first = DateOnly.FromDateTime(Local(start));
        var last = DateOnly.FromDateTime(Local(end.AddTicks(-1)));
        return employee.NgayVaoLam <= first && (!employee.NgayThoiViec.HasValue || employee.NgayThoiViec > last)
            && employee.HopDongs.Any(h => h.TrangThai == "HIEU_LUC" && h.NgayBatDau <= first && (!h.NgayKetThuc.HasValue || h.NgayKetThuc >= last));
    }
    public static bool IsMoney(decimal value) => value >= 0 && value <= 9999999999999.99m && decimal.Round(value, 2) == value;
    public static bool CanOperate(string role, int employeeId, int managerId) =>
        role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("HR", StringComparison.OrdinalIgnoreCase)
        || (role.Equals("Manager", StringComparison.OrdinalIgnoreCase) && employeeId == managerId);
    public static bool CanTransition(string current, string next, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) =>
        current is ("LEN_KE_HOACH" or "DANG_DIEN_RA" or "CHO_NGHIEM_THU") && (next == "HUY"
        || (current == "LEN_KE_HOACH" && next == "DANG_DIEN_RA" && now >= start)
        || (current == "DANG_DIEN_RA" && next == "CHO_NGHIEM_THU" && now >= end));
    public static bool ReviewsReady(IEnumerable<PhanCongDuAn> assignments, decimal budget)
    {
        decimal total = 0;
        foreach (var item in assignments)
        {
            if (item.NgayDanhGia is null || item.DanhGia is not ("HOAN_THANH" or "XUAT_SAC" or "KHONG_THAM_GIA")
                || !IsMoney(item.SoTienPhuCap) || (item.DanhGia == "KHONG_THAM_GIA" && item.SoTienPhuCap != 0)) return false;
            total += item.SoTienPhuCap;
            if (total > budget) return false;
        }
        return IsMoney(budget);
    }
}

public record EventDepartmentStatus(
    string Code,
    string Name,
    int AssignedCount,
    bool IsSufficient,
    IReadOnlyList<PhanCongDuAn> Assignments
);

public record EventStaffingSummary(
    int TotalRequired,
    int SatisfiedCount,
    bool IsFullyStaffed,
    IReadOnlyList<string> MissingDepartmentNames,
    IReadOnlyList<EventDepartmentStatus> Departments,
    string SummaryText
);
