using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public static class HotelEventRules
{
    public const string HotelVenueName = "Tại khách sạn";

    public static int CountEffectiveStaff(IEnumerable<PhanCongDuAn> assignments) => assignments
        .Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY"))
        .Select(p => p.MaNhanVien).Distinct().Count();

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
            return null;
        }
        // Old snapshots may contain only the original installation's numeric department ID.
        if (maPhongBan == 2) return "TIEN_SANH";
        if (maPhongBan == 4) return "AM_THUC";
        if (maPhongBan == 5) return "BEP";
        if (maPhongBan == 3) return "BUONG_PHONG";
        if (maPhongBan == 6) return "KY_THUAT";
        if (maPhongBan == 7) return "AN_NINH";
        if (maPhongBan == 9) return "IT";
        return null;
    }

    public static string? ResolveDepartmentCode(NhanVien? employee) =>
        employee == null ? null : ResolveDepartmentCode(employee.MaPhongBan, employee.PhongBan?.TenPhongBan);

    public static IReadOnlyList<string> GetRequiredDepartments(string eventType) =>
        RequiredDepartmentsByType.GetValueOrDefault(eventType, []);

    public static EventStaffingSummary GetStaffingSummary(string eventType, IEnumerable<PhanCongDuAn> assignments)
    {
        var requiredCodes = GetRequiredDepartments(eventType);
        var assignmentList = assignments.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG").ToList();
        var list = new List<EventDepartmentStatus>();
        var missing = new List<string>();
        int satisfied = 0;

        foreach (var code in requiredCodes)
        {
            var name = DepartmentLabels.GetValueOrDefault(code, code);
            var deptAssignments = assignmentList.Where(p => (p.MaPhongBanSuKien ?? ResolveDepartmentCode(p.NhanVien)) == code).ToList();
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

    public static EventStaffingSummary GetStaffingSummary(DuAn item, DateTimeOffset? coverageStart = null)
    {
        if (item.TinhTheoGio || item.NhuCaus.Count > 0) return DeclaredStaffing(item, coverageStart);
        var baseline = GetStaffingSummary(item.LoaiDuAn, item.PhanCongs);
        var start = coverageStart is { } from && from > item.NgayBatDau ? from : item.NgayBatDau;
        var missingRoles = new List<string>();
        var departments = baseline.Departments.Select(department =>
        {
            var roles = HotelEventRoleRules.GetRoles(item.LoaiDuAn, department.Code, item.SoKhachDuKien).Select(rule =>
            {
                var assigned = department.Assignments.Where(p => HotelEventRoleRules.Matches(rule.Name, p.VaiTroDuAn)).ToList();
                var concurrent = HotelEventRoleRules.MinimumConcurrentCount(assigned, start, item.NgayKetThuc);
                var missing = Math.Max(0, rule.RequiredCount - concurrent);
                if (missing > 0) missingRoles.Add($"{department.Name}: {rule.Name} (thiếu {missing} người)");
                return new EventRoleStaffingStatus(rule.Name, rule.RequiredCount, rule.IsRequired,
                    assigned.Select(p => p.MaNhanVien).Distinct().Count(), concurrent, missing, assigned);
            }).ToList();
            return department with { Roles = roles, IsSufficient = item.SoKhachDuKien > 0 && roles.Count > 0 && roles.All(r => r.MissingCount == 0) };
        }).ToList();
        var satisfied = departments.Count(d => d.IsSufficient);
        var full = departments.Count > 0 && satisfied == departments.Count;
        var message = item.SoKhachDuKien == 0 ? "Chưa xác định quy mô. Nhập số khách trước khi bắt đầu sự kiện."
            : full ? $"Đã đủ vai trò bắt buộc {satisfied}/{departments.Count} phòng ban."
            : $"Đủ vai trò {satisfied}/{departments.Count} phòng ban; còn thiếu nhân sự theo vai trò và thời gian tham gia.";
        return new EventStaffingSummary(departments.Count, satisfied, full,
            departments.Where(d => !d.IsSufficient).Select(d => d.Name).ToList(), departments, message)
        { MissingRoleDescriptions = missingRoles };
    }

    private static EventStaffingSummary DeclaredStaffing(DuAn item, DateTimeOffset? coverageStart)
    {
        var active = item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG").ToList();
        var missing = new List<string>();
        var departments = item.NhuCaus.GroupBy(n => n.MaPhongBan).Select(group =>
        {
            var name = DepartmentLabels.GetValueOrDefault(group.Key, group.Key);
            var members = active.Where(p => (p.MaPhongBanSuKien ?? ResolveDepartmentCode(p.NhanVien)) == group.Key).ToList();
            var roles = group.OrderBy(n => n.BatDau).Select(need =>
            {
                var from = coverageStart is { } start && start > need.BatDau ? start : need.BatDau;
                var to = item.KetThucThucTe is { } stopped && stopped < need.KetThuc ? stopped : need.KetThuc;
                var assigned = members.Where(p => HotelEventRoleRules.Matches(need.VaiTro, p.VaiTroDuAn)).ToList();
                var concurrent = from >= to ? need.SoNguoi : HotelEventRoleRules.MinimumConcurrentCount(assigned, from, to);
                var shortfall = Math.Max(0, need.SoNguoi - concurrent);
                if (shortfall > 0) missing.Add($"{name}: {need.VaiTro} thiếu {shortfall} ({Local(from):dd/MM HH:mm}–{Local(to):dd/MM HH:mm})");
                return new EventRoleStaffingStatus(need.VaiTro, need.SoNguoi, need.SoNguoi > 0,
                    assigned.Select(p => p.MaNhanVien).Distinct().Count(), concurrent, shortfall, assigned);
            }).ToList();
            return new EventDepartmentStatus(group.Key, name, members.Select(p => p.MaNhanVien).Distinct().Count(),
                roles.All(r => r.MissingCount == 0), members) { Roles = roles };
        }).ToList();
        var satisfied = departments.Count(d => d.IsSufficient);
        var full = departments.Count > 0 && item.NhuCaus.Any(n => n.SoNguoi > 0) && missing.Count == 0;
        var message = item.NhuCaus.Count == 0 ? "Chưa khai báo nhu cầu nhân sự."
            : full ? "Đã đủ nhân sự theo các khung nhiệm vụ."
            : $"Đã ghi nhận nhu cầu; đủ {satisfied}/{departments.Count} phòng ban. Chờ bố trí ca/phân công cho các khung còn thiếu.";
        return new EventStaffingSummary(departments.Count, satisfied, full,
            departments.Where(d => !d.IsSufficient).Select(d => d.Name).ToList(), departments, message)
            { MissingRoleDescriptions = missing };
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
    public const string AutoCancelReason = "Không liên lạc được với người tổ chức sự kiện";
    public static bool NeedsLateConfirmationWarning(DuAn item, DateTimeOffset now) => item.TrangThai == "LEN_KE_HOACH" && now >= item.NgayBatDau;
    public static bool IsExpiredPlanning(DuAn item, DateTimeOffset now) =>
        item.TrangThai == "LEN_KE_HOACH" && DateOnly.FromDateTime(Local(item.NgayBatDau)) < DateOnly.FromDateTime(Local(now));
    public static bool CanTransition(string current, string next, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) =>
        current is ("LEN_KE_HOACH" or "DANG_DIEN_RA" or "CHO_NGHIEM_THU") && ((next == "HUY" && current == "LEN_KE_HOACH")
        || (current == "LEN_KE_HOACH" && next == "DANG_DIEN_RA" && now >= start && now <= end)
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

    public static readonly IReadOnlyDictionary<(string EventType, string DeptCode), decimal> StandardBonusMatrix =
        new Dictionary<(string EventType, string DeptCode), decimal>
        {
            // Đoàn VIP (5 phòng ban)
            [("DOAN_VIP", "BEP")] = 800_000m,
            [("DOAN_VIP", "AM_THUC")] = 700_000m,
            [("DOAN_VIP", "AN_NINH")] = 700_000m,
            [("DOAN_VIP", "TIEN_SANH")] = 600_000m,
            [("DOAN_VIP", "BUONG_PHONG")] = 600_000m,

            // Festival ẩm thực (5 phòng ban)
            [("FESTIVAL_AM_THUC", "BEP")] = 700_000m,
            [("FESTIVAL_AM_THUC", "AM_THUC")] = 500_000m,
            [("FESTIVAL_AM_THUC", "KY_THUAT")] = 500_000m,
            [("FESTIVAL_AM_THUC", "BUONG_PHONG")] = 400_000m,
            [("FESTIVAL_AM_THUC", "AN_NINH")] = 400_000m,

            // Gala Dinner (5 phòng ban)
            [("GALA_DINNER", "BEP")] = 700_000m,
            [("GALA_DINNER", "AM_THUC")] = 600_000m,
            [("GALA_DINNER", "KY_THUAT")] = 600_000m,
            [("GALA_DINNER", "TIEN_SANH")] = 400_000m,
            [("GALA_DINNER", "AN_NINH")] = 400_000m,

            // Hội nghị MICE (6 phòng ban)
            [("HOI_NGHI_MICE", "IT")] = 700_000m,
            [("HOI_NGHI_MICE", "KY_THUAT")] = 600_000m,
            [("HOI_NGHI_MICE", "BEP")] = 500_000m,
            [("HOI_NGHI_MICE", "AM_THUC")] = 400_000m,
            [("HOI_NGHI_MICE", "TIEN_SANH")] = 400_000m,
            [("HOI_NGHI_MICE", "AN_NINH")] = 400_000m,

            // Tiệc cưới (6 phòng ban)
            [("TIEC_CUOI", "BEP")] = 600_000m,
            [("TIEC_CUOI", "AM_THUC")] = 500_000m,
            [("TIEC_CUOI", "KY_THUAT")] = 500_000m,
            [("TIEC_CUOI", "TIEN_SANH")] = 400_000m,
            [("TIEC_CUOI", "AN_NINH")] = 400_000m,
            [("TIEC_CUOI", "BUONG_PHONG")] = 300_000m,
        };

    public static decimal AcceptanceAmount(DuAn item, PhanCongDuAn assignment,
        IEnumerable<CauHinhKhoanLuong>? bonusConfigs = null)
    {
        if (assignment.DanhGia == "KHONG_THAM_GIA") return 0;
        if (assignment.DanhGia is not ("HOAN_THANH" or "XUAT_SAC"))
            throw new ArgumentException("Nhân viên chưa được đánh giá.", nameof(assignment));
        return ResolveEventBonus(item, assignment.NhanVien?.PhongBan, assignment, bonusConfigs, assignment.MaPhongBanSuKien).SoTien;
    }

    public static (int? MaKhoan, string TenKhoan, decimal SoTien) ResolveEventBonus(
        DuAn? duAn,
        PhongBan? phongBan,
        PhanCongDuAn phanCong,
        IEnumerable<CauHinhKhoanLuong>? bonusConfigs = null,
        string? departmentCode = null)
    {
        if (duAn?.TrangThai == "HOAN_THANH")
            return (null, $"Thưởng sự kiện SK-{duAn.MaDuAn} - {phanCong.VaiTroDuAn}", phanCong.SoTienPhuCap);
        var eventType = duAn?.LoaiDuAn?.ToUpperInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(eventType) || !TypeLabels.ContainsKey(eventType))
        {
            var tenDuAn = duAn?.TenDuAn ?? "";
            if (tenDuAn.Contains("VIP", StringComparison.OrdinalIgnoreCase)) eventType = "DOAN_VIP";
            else if (tenDuAn.Contains("Cưới", StringComparison.OrdinalIgnoreCase) || tenDuAn.Contains("Cuoi", StringComparison.OrdinalIgnoreCase) || tenDuAn.Contains("Đám cưới", StringComparison.OrdinalIgnoreCase)) eventType = "TIEC_CUOI";
            else if (tenDuAn.Contains("MICE", StringComparison.OrdinalIgnoreCase) || tenDuAn.Contains("Hội nghị", StringComparison.OrdinalIgnoreCase)) eventType = "HOI_NGHI_MICE";
            else if (tenDuAn.Contains("Gala", StringComparison.OrdinalIgnoreCase)) eventType = "GALA_DINNER";
            else if (tenDuAn.Contains("Festival", StringComparison.OrdinalIgnoreCase) || tenDuAn.Contains("Ẩm thực", StringComparison.OrdinalIgnoreCase)) eventType = "FESTIVAL_AM_THUC";
        }

        var deptCode = departmentCode ?? ResolveDepartmentCode(phongBan?.MaPhongBan, phongBan?.TenPhongBan);

        // 1. Tìm trong cấu hình DB cau_hinh_khoan_luong
        if (!string.IsNullOrEmpty(eventType) && !string.IsNullOrEmpty(deptCode) && bonusConfigs != null)
        {
            var eventKeyword = eventType switch
            {
                "DOAN_VIP" => "Đoàn VIP",
                "TIEC_CUOI" => "Tiệc cưới",
                "HOI_NGHI_MICE" => "MICE",
                "GALA_DINNER" => "Gala Dinner",
                "FESTIVAL_AM_THUC" => "Festival ẩm thực",
                _ => TypeLabels.GetValueOrDefault(eventType, eventType)
            };

            var deptKeyword = deptCode switch
            {
                "TIEN_SANH" => "Tiền sảnh",
                "AM_THUC" => "F&B",
                "BEP" => "Bếp",
                "BUONG_PHONG" => "Buồng phòng",
                "KY_THUAT" => "Kỹ thuật",
                "AN_NINH" => "An ninh",
                "IT" => "IT",
                _ => ""
            };

            var matched = bonusConfigs.FirstOrDefault(k =>
                k.LoaiKhoan == "THUONG" &&
                k.TenKhoan.Contains(eventKeyword, StringComparison.OrdinalIgnoreCase) &&
                k.TenKhoan.Contains(deptKeyword, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                return (matched.MaKhoan, matched.TenKhoan, matched.GiaTriMacDinh);
            }
        }

        // 2. Tra cứu định mức tiêu chuẩn theo ma trận mặc định nếu có cặp (EventType, DeptCode)
        if (!string.IsNullOrEmpty(eventType) && !string.IsNullOrEmpty(deptCode) &&
            StandardBonusMatrix.TryGetValue((eventType, deptCode), out var standardAmount))
        {
            var eventName = TypeLabels.GetValueOrDefault(eventType, eventType);
            var deptName = DepartmentLabels.GetValueOrDefault(deptCode, deptCode);
            return (null, $"Thưởng {eventName} - {deptName}", standardAmount);
        }

        // 3. Fallback cho dự án ngoại lệ ngoài ma trận 27 định mức
        var fallbackAmount = phanCong.SoTienPhuCap > 0 ? phanCong.SoTienPhuCap : phanCong.PhuCapDuKien;
        var defaultTitle = $"Thưởng sự kiện: {duAn?.TenDuAn ?? "Sự kiện"} - {phanCong.VaiTroDuAn}";
        return (null, defaultTitle, fallbackAmount);
    }
}

public record EventDepartmentStatus(
    string Code,
    string Name,
    int AssignedCount,
    bool IsSufficient,
    IReadOnlyList<PhanCongDuAn> Assignments
)
{
    public IReadOnlyList<EventRoleStaffingStatus> Roles { get; init; } = [];
    public int RequiredCount => Roles.Sum(r => r.RequiredCount);
    public int MissingCount => Roles.Sum(r => r.MissingCount);
}

public record EventStaffingSummary(
    int TotalRequired,
    int SatisfiedCount,
    bool IsFullyStaffed,
    IReadOnlyList<string> MissingDepartmentNames,
    IReadOnlyList<EventDepartmentStatus> Departments,
    string SummaryText
)
{
    public IReadOnlyList<string> MissingRoleDescriptions { get; init; } = [];
}
