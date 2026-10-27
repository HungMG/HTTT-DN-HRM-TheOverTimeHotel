using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Core.Policies;

public static class HotelEventRoleRules
{
    public const string OtherRoleValue = "__OTHER__";
    public const string OtherRolePrefix = "Khác: ";

    private sealed record DepartmentRoles(int Capacity, string Primary, string Support, string Optional);
    private static readonly IReadOnlyDictionary<(string Type, string Department), DepartmentRoles> Catalog =
        new Dictionary<(string, string), DepartmentRoles>
        {
            [("TIEC_CUOI","TIEN_SANH")] = new(100,"Lễ tân đón khách","Hướng dẫn chỗ ngồi","Điều phối sảnh"),
            [("TIEC_CUOI","AM_THUC")] = new(20,"Phục vụ bàn tiệc","Phục vụ đồ uống","Điều phối phục vụ"),
            [("TIEC_CUOI","BEP")] = new(50,"Chế biến món chính","Chuẩn bị món khai vị","Chuẩn bị món tráng miệng"),
            [("TIEC_CUOI","BUONG_PHONG")] = new(100,"Vệ sinh sảnh tiệc","Chuẩn bị khăn và đồ vải","Chuẩn bị khu vực tổ chức"),
            [("TIEC_CUOI","KY_THUAT")] = new(200,"Phụ trách âm thanh","Phụ trách ánh sáng","Trực điện và thiết bị"),
            [("TIEC_CUOI","AN_NINH")] = new(100,"Kiểm soát lối vào","Tuần tra khu vực tiệc","Hướng dẫn bãi xe"),
            [("HOI_NGHI_MICE","TIEN_SANH")] = new(75,"Tiếp nhận đăng ký","Đón tiếp đại biểu","Hướng dẫn phòng hội nghị"),
            [("HOI_NGHI_MICE","AM_THUC")] = new(40,"Phục vụ tiệc trà","Phục vụ đồ uống","Phục vụ tiệc hội nghị"),
            [("HOI_NGHI_MICE","BEP")] = new(80,"Chế biến suất ăn","Chuẩn bị tiệc trà","Chuẩn bị tiệc buffet"),
            [("HOI_NGHI_MICE","KY_THUAT")] = new(100,"Phụ trách âm thanh và micro","Trực thiết bị hội trường","Phụ trách ánh sáng"),
            [("HOI_NGHI_MICE","AN_NINH")] = new(100,"Kiểm soát thẻ đại biểu","Bảo đảm an ninh hội trường","Điều phối xe"),
            [("HOI_NGHI_MICE","IT")] = new(100,"Phụ trách mạng và Wi-Fi","Hỗ trợ máy tính và trình chiếu","Hỗ trợ hội nghị trực tuyến"),
            [("GALA_DINNER","TIEN_SANH")] = new(100,"Lễ tân đón khách","Hướng dẫn bàn tiệc","Điều phối khu vực đón tiếp"),
            [("GALA_DINNER","AM_THUC")] = new(20,"Phục vụ bàn tiệc","Phục vụ quầy đồ uống","Điều phối phục vụ"),
            [("GALA_DINNER","BEP")] = new(50,"Chế biến món tiệc","Trình bày món ăn","Chuẩn bị món tráng miệng"),
            [("GALA_DINNER","KY_THUAT")] = new(100,"Phụ trách âm thanh sân khấu","Phụ trách ánh sáng","Trực thiết bị sân khấu"),
            [("GALA_DINNER","AN_NINH")] = new(100,"Kiểm soát khách vào","Bảo đảm an ninh sân khấu","Tuần tra khu vực tổ chức"),
            [("FESTIVAL_AM_THUC","AM_THUC")] = new(25,"Phục vụ gian hàng","Phục vụ đồ uống","Hướng dẫn khách tại khu ẩm thực"),
            [("FESTIVAL_AM_THUC","BEP")] = new(30,"Chế biến tại quầy","Phụ trách gian bếp","Giới thiệu món ăn"),
            [("FESTIVAL_AM_THUC","BUONG_PHONG")] = new(100,"Vệ sinh khu ẩm thực","Thu gom rác","Vệ sinh khu vực dùng chung"),
            [("FESTIVAL_AM_THUC","KY_THUAT")] = new(150,"Phụ trách điện gian hàng","Trực thiết bị gian hàng","Phụ trách âm thanh"),
            [("FESTIVAL_AM_THUC","AN_NINH")] = new(100,"Kiểm soát lối vào","Điều phối dòng khách","Tuần tra khu vực gian hàng"),
            [("DOAN_VIP","TIEN_SANH")] = new(20,"Đón và tiễn khách VIP","Điều phối lịch trình","Hỗ trợ yêu cầu của đoàn"),
            [("DOAN_VIP","AM_THUC")] = new(10,"Phục vụ bàn VIP","Phục vụ đồ uống VIP","Điều phối phục vụ riêng"),
            [("DOAN_VIP","BEP")] = new(25,"Chế biến món VIP","Phụ trách thực đơn riêng","Kiểm tra trình bày món"),
            [("DOAN_VIP","BUONG_PHONG")] = new(15,"Chuẩn bị phòng VIP","Kiểm tra chất lượng phòng","Hỗ trợ yêu cầu tại phòng"),
            [("DOAN_VIP","AN_NINH")] = new(20,"Kiểm soát khu vực VIP","Bảo đảm tuyến di chuyển","Điều phối xe đón tiễn")
        };

    public static IReadOnlyList<EventRoleRequirement> GetRoles(string eventType, string department, int guests)
    {
        if (guests < 0) throw new ArgumentOutOfRangeException(nameof(guests));
        if (!Catalog.TryGetValue((eventType, department), out var rules)) return [];
        return [new(rules.Primary, (int)(((long)guests + rules.Capacity - 1) / rules.Capacity), true),
            new(rules.Support, guests > 0 ? 1 : 0, true), new(rules.Optional, 0, false)];
    }

    public static string? ResolveRole(string eventType, string department, string? selected, string? custom)
    {
        if (!Catalog.ContainsKey((eventType, department))) return null;
        var role = selected?.Trim();
        if (role == OtherRoleValue)
        {
            var name = custom?.Trim();
            return !string.IsNullOrWhiteSpace(name) && name.Length + OtherRolePrefix.Length <= 100
                ? OtherRolePrefix + name : null;
        }
        return GetRoles(eventType, department, 0).FirstOrDefault(r => Matches(r.Name, role))?.Name;
    }

    public static bool Matches(string expected, string? actual) =>
        string.Equals(expected, actual?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static int MinimumConcurrentCount(IEnumerable<PhanCongDuAn> assignments, DateTimeOffset start, DateTimeOffset end)
    {
        if (start >= end) return 0;
        var points = new SortedDictionary<DateTimeOffset, List<(int Employee, int Delta)>> { [start] = [], [end] = [] };
        foreach (var p in assignments)
        {
            if (p.TrangThaiThamGia == "HUY_PHAN_CONG") continue;
            var from = p.BatDau > start ? p.BatDau : start;
            var to = p.KetThuc < end ? p.KetThuc : end;
            if (from >= to) continue;
            if (!points.ContainsKey(from)) points[from] = [];
            if (!points.ContainsKey(to)) points[to] = [];
            points[from].Add((p.MaNhanVien, 1));
            points[to].Add((p.MaNhanVien, -1));
        }
        var active = new Dictionary<int, int>();
        var minimum = int.MaxValue;
        foreach (var (instant, changes) in points)
        {
            if (instant == end) break;
            foreach (var (employee, delta) in changes)
            {
                var count = active.GetValueOrDefault(employee) + delta;
                if (count == 0) active.Remove(employee);
                else active[employee] = count;
            }
            minimum = Math.Min(minimum, active.Count);
        }
        return minimum;
    }

    public static bool CanRemove(DuAn item, int assignmentId, DateTimeOffset now)
    {
        if (item.TinhTheoGio || item.NhuCaus.Count > 0)
        {
            var remainingEvent = new DuAn { TinhTheoGio = true, LoaiDuAn = item.LoaiDuAn, NgayBatDau = item.NgayBatDau,
                NgayKetThuc = item.NgayKetThuc, KetThucThucTe = item.KetThucThucTe, NhuCaus = item.NhuCaus,
                PhanCongs = item.PhanCongs.Where(p => p.MaPhanCong != assignmentId).ToList() };
            return item.PhanCongs.Any(p => p.MaPhanCong == assignmentId) && HotelEventRules.GetStaffingSummary(remainingEvent, now).IsFullyStaffed;
        }
        var removed = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == assignmentId);
        if (removed == null) return false;
        var department = removed.MaPhongBanSuKien ?? HotelEventRules.ResolveDepartmentCode(removed.NhanVien);
        var role = GetRoles(item.LoaiDuAn, department ?? "", item.SoKhachDuKien)
            .FirstOrDefault(r => Matches(r.Name, removed.VaiTroDuAn));
        if (role is not { IsRequired: true }) return true;
        var start = new[] { now, item.NgayBatDau, removed.BatDau }.Max();
        var end = removed.KetThuc < item.NgayKetThuc ? removed.KetThuc : item.NgayKetThuc;
        if (start >= end) return true;
        var remaining = item.PhanCongs.Where(p => p.MaPhanCong != assignmentId
            && p.TrangThaiThamGia != "HUY_PHAN_CONG" && (p.MaPhongBanSuKien ?? HotelEventRules.ResolveDepartmentCode(p.NhanVien)) == department && Matches(role.Name, p.VaiTroDuAn));
        return MinimumConcurrentCount(remaining, start, end) >= role.RequiredCount;
    }
}

public record EventRoleRequirement(string Name, int RequiredCount, bool IsRequired);
public record EventRoleStaffingStatus(string Name, int RequiredCount, bool IsRequired,
    int AssignedCount, int MinimumConcurrentCount, int MissingCount, IReadOnlyList<PhanCongDuAn> Assignments);
