using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

public sealed partial class HotelEventService(AppDbContext db) : IHotelEventService
{
    private IQueryable<DuAn> EventInformation => db.DuAns.Include(e => e.NguoiQuanLy)
        .Include(e => e.NhuCaus).Include(e => e.QuyetToans).AsSplitQuery();
    private IQueryable<DuAn> Events => EventInformation
        .Include(e => e.PhanCongs).ThenInclude(p => p.NhanVien).ThenInclude(n => n!.PhongBan)
        .Include(e => e.PhanCongs).ThenInclude(p => p.NhanVien).ThenInclude(n => n!.HopDongs)
        .Include(e => e.PhanCongs).ThenInclude(p => p.PhanCa).ThenInclude(p => p!.ChamCong)
        .Include(e => e.PhanCongs).ThenInclude(p => p.CongThucTes);

    public Task<int> AutoCancelExpiredEventsAsync() => Task.FromResult(0);
    private async Task<int> DeprecatedAutoCancelExpiredEventsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var startOfTodayLocal = DateOnly.FromDateTime(HotelEventRules.Local(now)).ToDateTime(TimeOnly.MinValue);
        var startOfTodayUtc = HotelEventRules.ToUtc(startOfTodayLocal);

        var expiredIds = await db.DuAns.AsNoTracking()
            .Where(e => e.TrangThai == "LEN_KE_HOACH" && e.NgayBatDau < startOfTodayUtc)
            .Select(e => e.MaDuAn)
            .ToListAsync();

        if (expiredIds.Count == 0) return 0;

        return await Write(async () =>
        {
            var items = await db.DuAns
                .Include(e => e.PhanCongs)
                .Where(e => expiredIds.Contains(e.MaDuAn) && e.TrangThai == "LEN_KE_HOACH")
                .ToListAsync();

            if (items.Count == 0) return 0;

            foreach (var item in items)
            {
                item.TrangThai = "HUY";
                item.LyDoHuy = HotelEventRules.AutoCancelReason;
                foreach (var assignment in item.PhanCongs)
                {
                    assignment.TrangThaiChiTra = "HUY_BO";
                    assignment.SoTienPhuCap = 0;
                    if (item.TinhTheoGio)
                    {
                        assignment.TrangThaiThamGia = "HUY_PHAN_CONG";
                        assignment.LyDoKetThuc = HotelEventRules.AutoCancelReason;
                    }
                }
                item.Version++;
                db.NhatKyQuanTris.Add(new NhatKyQuanTri
                {
                    MaTaiKhoanThucHien = null,
                    TenNguoiThucHien = "Hệ thống",
                    TenTaiKhoanBiTacDong = $"SK-{item.MaDuAn}",
                    HanhDong = "TU_DONG_HUY_SU_KIEN",
                    NoiDung = "Tự động hủy sự kiện do quá ngày bắt đầu nhưng chưa diễn ra.",
                    GiaTriMoi = $"version={item.Version}; trang_thai=HUY",
                    LyDo = HotelEventRules.AutoCancelReason,
                    ThoiGian = DateTimeOffset.UtcNow
                });
            }

            return items.Count;
        });
    }

public async Task<IReadOnlyList<DuAn>> ListAsync(EventActor actor, DateTimeOffset from, DateTimeOffset to)
    {
        // Auto-cancel on read removed per plan
        actor = await CurrentActor(actor);
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        return await Scoped(Events.AsNoTracking(), actor)
            .Where(e => e.NgayBatDau < to
                && (e.KetThucThucTe ?? e.NgayKetThuc) > from).OrderBy(e => e.NgayBatDau).ToListAsync();
    }

    public async Task<DuAn?> GetAsync(EventActor actor, int id)
    {
        // Auto-cancel on read removed per plan
        actor = await CurrentActor(actor);
        return await Scoped(Events.AsNoTracking(), actor).SingleOrDefaultAsync(e => e.MaDuAn == id);
    }

    public async Task<DuAn?> GetOverviewAsync(EventActor actor, int id)
    {
        actor = await CurrentActor(actor);
        return await Scoped(EventInformation.AsNoTracking(), actor).SingleOrDefaultAsync(e => e.MaDuAn == id);
    }

    public Task<decimal> HourlyEventRateAsync() => EventRateConfig.ReadAsync(db);

    public async Task<IReadOnlyList<NhanVien>> ManagersAsync() => await db.NhanViens.AsNoTracking()
        .Where(n => n.TrangThai == "DANG_LAM" && n.TaiKhoan != null && n.TaiKhoan.TrangThai
            && n.TaiKhoan.VaiTro != null && n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "HR")
        .OrderBy(n => n.Ten).ThenBy(n => n.Ho).ToListAsync();

    public async Task<IReadOnlyList<NhanVien>> EmployeesAsync() => await db.NhanViens.AsNoTracking()
        .Include(n => n.PhongBan).Include(n => n.HopDongs).Where(n => n.TrangThai == "DANG_LAM")
        .OrderBy(n => n.Ten).ThenBy(n => n.Ho).ToListAsync();

    public async Task<IReadOnlyList<PhanCa>> ShiftsAsync(DuAn item)
    {
        var ids = item.PhanCongs.Select(p => p.MaNhanVien).ToArray();
        return await db.PhanCas.AsNoTracking().Include(p => p.NhanVien).Include(p => p.CaLamViec)
            .Where(p => ids.Contains(p.MaNhanVien) && p.TrangThai != "DA_HUY"
                && p.BatDauDuKien < item.NgayKetThuc && p.KetThucDuKien > item.NgayBatDau)
            .OrderBy(p => p.BatDauDuKien).ToListAsync();
    }

    public async Task<IReadOnlyList<NhatKyQuanTri>> HistoryAsync(int id) => await db.NhatKyQuanTris.AsNoTracking()
        .Where(n => n.TenTaiKhoanBiTacDong == $"SK-{id}").OrderByDescending(n => n.ThoiGian).ToListAsync();

    public Task<int> SaveAsync(EventActor actor, EventInput input) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR hoặc Admin được lập và sửa sự kiện.", true);
        DuAn item;
        if (input.Id == 0) item = new DuAn();
        else
        {
            item = await Editable(actor, input.Id, input.Version);
            Require(item.TrangThai == "LEN_KE_HOACH", "", "Chỉ sửa thông tin khi sự kiện đang lên kế hoạch.");
        }
        var name = Text(input.Name, 200, "Input.Name", "Tên sự kiện");
        var hourly = input.Id == 0 ? input.HourlyCompensation : item.TinhTheoGio;
        Require(HotelEventRules.CreationTypeLabels.ContainsKey(input.Type)
            || input.Id > 0 && input.Type == item.LoaiDuAn && HotelEventRules.TypeLabels.ContainsKey(input.Type),
            "Input.Type", "Chọn loại sự kiện đang sử dụng. Loại cũ chỉ được giữ nguyên trong hồ sơ đã có.");
        var start = Utc(input.Start, "Input.Start");
        var end = Utc(input.End, "Input.End");
        Require(input.Id != 0 || HotelEventRules.CanCreateOn(DateOnly.FromDateTime(input.Start), DateTimeOffset.UtcNow),
            "Input.Start", "Chỉ được tạo sự kiện từ hôm nay trở đi (giờ Việt Nam).");
        Require(end > start, "Input.End", "Kết thúc phải sau thời gian bắt đầu.");
        var calculateAtAcceptance = hourly || (input.Id == 0 ? input.CalculateBudgetAtAcceptance : item.TinhPhuCapKhiNghiemThu);
        var budget = calculateAtAcceptance ? 0m : input.Budget;
        Require(HotelEventRules.IsMoney(budget), "Input.Budget", "Ngân sách phải không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(input.ExpectedGuests >= 0, "Input.ExpectedGuests", "Số khách dự kiến không được âm.");
        Require(input.Description is null || input.Description.Length <= 10000, "Input.Description", "Mô tả tối đa 10.000 ký tự.");
        Require(!await db.DuAns.AnyAsync(e => e.MaDuAn != input.Id && e.TenDuAn == name), "Input.Name", "Tên sự kiện đã được sử dụng.");
        Require(await db.NhanViens.AnyAsync(n => n.MaNhanVien == input.ManagerId && n.TrangThai == "DANG_LAM"
            && n.TaiKhoan != null && n.TaiKhoan.TrangThai && n.TaiKhoan.VaiTro != null
            && n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "HR"),
            "Input.ManagerId", "Chọn nhân viên HR đang làm việc có tài khoản đang hoạt động.");
        Require(!item.PhanCongs.Any(p => p.TrangThaiThamGia != "HUY_PHAN_CONG") || (item.NgayBatDau == start && item.NgayKetThuc == end), "Input.Start",
            "Gỡ các phân công trước khi thay đổi thời gian sự kiện.");
        if (input.Id != 0 && input.Type != item.LoaiDuAn)
        {
            var newAllowed = HotelEventRules.GetAllowedDepartments(input.Type);
            var incompatible = item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG")
                .Where(p => {
                    var code = p.MaPhongBanSuKien ?? HotelEventRules.ResolveDepartmentCode(p.NhanVien);
                    return code == null || !newAllowed.Contains(code)
                        || (!p.VaiTroDuAn.StartsWith(HotelEventRoleRules.OtherRolePrefix, StringComparison.Ordinal)
                            && HotelEventRoleRules.ResolveRole(input.Type, code, p.VaiTroDuAn, null) == null);
                })
                .ToList();
            Require(incompatible.Count == 0, "Input.Type",
                $"Không thể đổi loại sự kiện: có {incompatible.Count} phân công có phòng ban hoặc vai trò không phù hợp. Vui lòng phân công lại trước khi đổi.");
        }
        Require(calculateAtAcceptance || item.PhanCongs.Sum(p => p.PhuCapDuKien) <= budget, "Input.Budget", "Ngân sách thấp hơn tổng phụ cấp đã phân công.");
        if (input.Id != 0 && item.NhuCaus.Count > 0)
            Require(item.NhuCaus.Where(n => !n.LaMau).All(n => HotelEventRules.GetAllowedDepartments(input.Type).Contains(n.MaPhongBan)
                && HotelEventRoleRules.ResolveRole(input.Type, n.MaPhongBan, n.VaiTro, null) != null), "Input.Start",
                "Nhu cầu không còn phù hợp. Điều chỉnh/xóa nhu cầu trước khi đổi loại sự kiện.");
        if (input.Id != 0 && (item.LoaiDuAn != input.Type || item.SoKhachDuKien != input.ExpectedGuests
            || item.NgayBatDau != start || item.NgayKetThuc != end))
        {
            foreach (var old in item.NhuCaus.Where(n => n.LaMau).ToList()) { db.Remove(old); item.NhuCaus.Remove(old); }
            foreach (var department in HotelEventRules.GetRequiredDepartments(input.Type))
                foreach (var role in HotelEventRoleRules.GetRoles(input.Type, department, input.ExpectedGuests).Where(r => r.IsRequired))
                    if (!item.NhuCaus.Any(n => n.MaPhongBan == department && n.VaiTro == role.Name))
                        item.NhuCaus.Add(new NhuCauSuKien { LaMau = true, MaPhongBan = department,
                            VaiTro = role.Name, SoNguoi = role.RequiredCount });
        }
        item.TenDuAn = name;
        item.LoaiDuAn = input.Type;
        item.DiaDiem = HotelEventRules.HotelVenueName;
        item.NgayBatDau = start;
        item.NgayKetThuc = end;
        item.NganSachPhuCap = budget;
        item.TinhPhuCapKhiNghiemThu = calculateAtAcceptance;
        item.TinhTheoGio = hourly;
        item.MaNguoiQuanLy = input.ManagerId;
        item.SoKhachDuKien = input.ExpectedGuests;
        item.MoTa = input.Description?.Trim();
        if (input.Id == 0)
        {
            db.DuAns.Add(item);
            foreach (var department in HotelEventRules.GetRequiredDepartments(item.LoaiDuAn))
                foreach (var role in HotelEventRoleRules.GetRoles(item.LoaiDuAn, department, item.SoKhachDuKien).Where(r => r.IsRequired))
                    item.NhuCaus.Add(new NhuCauSuKien { LaMau = true, MaPhongBan = department,
                        VaiTro = role.Name, SoNguoi = role.RequiredCount });
        }
        else item.Version++;
        await db.SaveChangesAsync(); // Identity is needed for the audit subject; still in the same transaction.
        Audit(actor, item, input.Id == 0 ? "TAO_SU_KIEN" : "SUA_SU_KIEN", input.Id == 0 ? "Tạo sự kiện." : "Cập nhật thông tin sự kiện.");
        return item.MaDuAn;
    });

    public Task AssignAsync(EventActor actor, int id, EventAssignmentInput input) =>
        AssignManyAsync(actor, id, input.Version, [input]);

    public Task AssignManyAsync(EventActor actor, int id, long version, IReadOnlyList<EventAssignmentInput> inputs) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        StaffingOpen(item);
        Require(inputs.Count > 0, "", "Chọn ít nhất một nhân viên.");
        foreach (var input in inputs)
            await AddAssignment(item, input);
        item.Version++;
        Audit(actor, item, "PHAN_CONG_SU_KIEN", inputs.Count == 1
            ? $"Phân công nhân viên #{inputs[0].EmployeeId}." : $"Phân công {inputs.Count} nhân viên.");
        return 0;
    });

    private async Task AddAssignment(DuAn item, EventAssignmentInput input)
    {
        var start = Utc(input.Start, "Assignment.Start");
        var end = Utc(input.End, "Assignment.End");
        Require(start >= item.NgayBatDau && start < end && end <= item.NgayKetThuc, "Assignment.End", "Thời gian tham gia phải nằm trong giờ bắt đầu đến kết thúc sự kiện.");
        PhanCa? source = null;
        if (item.TinhTheoGio)
        {
            var candidates = await db.PhanCas.Where(s => s.MaNhanVien == input.EmployeeId && s.DaCongBo
                && s.TrangThai != "DA_HUY" && s.TrangThai != "VANG_MAT" && s.BatDauDuKien < end && s.KetThucDuKien > start
                && (!input.ShiftId.HasValue || s.MaPhanCa == input.ShiftId)).ToListAsync();
            Require(candidates.Count == 1, "Assignment.ShiftId", "Chọn đúng một ca nguồn đã công bố giao với thời gian nhiệm vụ.");
            source = candidates[0];
            var window = HourlyEventRules.Intersect(start, end, source.BatDauDuKien, source.KetThucDuKien)!.Value;
            start = window.Start; end = window.End;
        }
        var allowance = item.TinhPhuCapKhiNghiemThu ? 0m : input.Allowance;
        Require(HotelEventRules.IsMoney(allowance), "Assignment.Allowance", "Phụ cấp không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(!item.PhanCongs.Any(p => p.MaNhanVien == input.EmployeeId && p.TrangThaiThamGia != "HUY_PHAN_CONG"
            && (!item.TinhTheoGio || HotelEventRules.Overlaps(start,end,p.BatDau,p.KetThuc))), "Assignment.EmployeeId", "Nhân viên đã được phân công/trùng nhiệm vụ trong sự kiện.");
        var employee = await db.NhanViens.Include(n => n.PhongBan).Include(n => n.HopDongs).SingleOrDefaultAsync(n => n.MaNhanVien == input.EmployeeId);
        Require(employee != null && HotelEventRules.ContractCovers(employee, start, end), "Assignment.EmployeeId", "Nhân viên phải đang làm việc và có hợp đồng hiệu lực suốt thời gian tham gia.");
        var allowedDepts = HotelEventRules.GetAllowedDepartments(item.LoaiDuAn);
        var empDept = HotelEventRules.ResolveDepartmentCode(employee);
        Require(empDept != null && allowedDepts.Contains(empDept), "Assignment.EmployeeId",
            $"Chỉ được chọn nhân viên thuộc các phòng ban quy định cho {HotelEventRules.TypeLabels.GetValueOrDefault(item.LoaiDuAn, "sự kiện")}.");
        var role = HotelEventRoleRules.ResolveRole(item.LoaiDuAn, empDept!, input.Role, input.OtherRole);
        Require(role != null, "Assignment.Role", "Chọn vai trò đúng loại sự kiện và phòng ban; vai trò khác cần tên không trống, tổng tối đa 100 ký tự.");
        Require(!await db.PhanCongDuAns.AnyAsync(p => p.MaNhanVien == input.EmployeeId && p.MaDuAn != item.MaDuAn
            && p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.DuAn!.TrangThai != "HUY" && p.BatDau < end && p.KetThuc > start), "Assignment.EmployeeId", "Nhân viên đã tham gia sự kiện khác trong khoảng thời gian này.");
        Require(item.TinhPhuCapKhiNghiemThu || item.PhanCongs.Sum(p => p.PhuCapDuKien) + allowance <= item.NganSachPhuCap, "Assignment.Allowance", "Tổng phụ cấp dự kiến vượt ngân sách sự kiện.");
        var assignment = new PhanCongDuAn
        {
            MaNhanVien = input.EmployeeId, VaiTroDuAn = role!, MaPhongBanSuKien = empDept,
            BatDau = start, KetThuc = end, PhuCapDuKien = allowance, MaPhanCa = source?.MaPhanCa, NhanVien = employee
        };
        if (item.TinhTheoGio) await ValidateSource(item, assignment);
        item.PhanCongs.Add(assignment);
    }

    public Task RemoveAssignmentAsync(EventActor actor, int id, int assignmentId, long version) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        StaffingOpen(item);
        var assignment = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == assignmentId);
        Require(assignment != null, "", "Không tìm thấy phân công.");
        Require(assignment!.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể gỡ phân công đã kết chuyển.");
        Require(assignment.CongThucTes.Count == 0 && (item.TrangThai == "LEN_KE_HOACH" || assignment.BatDau > DateTimeOffset.UtcNow), "",
            "Phân công đã có thể phát sinh công; dùng Kết thúc tham gia để giữ quyền lợi.");
        if (item.TrangThai == "DANG_DIEN_RA")
        {
            Require(HotelEventRoleRules.CanRemove(item, assignmentId, DateTimeOffset.UtcNow), "",
                "Không thể gỡ người làm thiếu vai trò bắt buộc trong thời gian còn lại. Phải thêm người thay thế cùng vai trò trước.");
            var deptCode = assignment.MaPhongBanSuKien ?? HotelEventRules.ResolveDepartmentCode(assignment.NhanVien);
            if (deptCode != null && HotelEventRules.GetRequiredDepartments(item.LoaiDuAn).Contains(deptCode))
            {
                var countInDept = item.PhanCongs.Count(p => p.TrangThaiThamGia != "HUY_PHAN_CONG"
                    && (p.MaPhongBanSuKien ?? HotelEventRules.ResolveDepartmentCode(p.NhanVien)) == deptCode);
                Require(countInDept > 1, "",
                    $"Không thể gỡ nhân viên cuối cùng của phòng ban {HotelEventRules.DepartmentLabels.GetValueOrDefault(deptCode, deptCode)} khi sự kiện đang diễn ra. Phải thêm người thay thế trước.");
            }
        }
        assignment.TrangThaiThamGia = "HUY_PHAN_CONG";
        assignment.LyDoKetThuc = "Hủy phân công trước khi thực hiện.";
        item.Version++;
        Audit(actor, item, "GO_PHAN_CONG_SU_KIEN", $"Gỡ phân công nhân viên #{assignment.MaNhanVien}.");
        return 0;
    });

    public Task ReviewAsync(EventActor actor, int id, EventReviewInput input) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, input.Version);
        ReviewOpen(item);
        var assignment = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == input.AssignmentId);
        Require(assignment != null, "Review.AssignmentId", "Không tìm thấy phân công trong sự kiện.");
        Require(input.Rating is "HOAN_THANH" or "XUAT_SAC" or "KHONG_THAM_GIA", "Review.Rating", "Chọn kết quả đánh giá hợp lệ.");
        Require(assignment!.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY"), "", "Phân công đã hủy; giữ lịch sử, không đánh giá trả tiền.");
        if (item.TinhTheoGio)
        {
            Require(input.Rating != "KHONG_THAM_GIA" || assignment.CongThucTes.Count == 0, "", "Đã có công thực tế; HR cần đối soát công trước khi ghi vắng mặt.");
            Require(input.Rating == "KHONG_THAM_GIA" || assignment.CongThucTes.Count > 0, "", "Ghi công thực tế trước khi đánh giá hoàn thành.");
        }
        var actualAllowance = item.TinhPhuCapKhiNghiemThu ? 0m : input.ActualAllowance;
        Require(HotelEventRules.IsMoney(actualAllowance), "Review.ActualAllowance", "Phụ cấp thực tế không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(input.Rating != "KHONG_THAM_GIA" || actualAllowance == 0, "Review.ActualAllowance", "Nhân viên không tham gia phải có phụ cấp bằng 0.");
        Require(item.TinhPhuCapKhiNghiemThu || item.PhanCongs.Where(p => p.MaPhanCong != input.AssignmentId).Sum(p => p.SoTienPhuCap) + actualAllowance <= item.NganSachPhuCap,
            "Review.ActualAllowance", "Tổng phụ cấp thực tế vượt ngân sách sự kiện.");
        Require(assignment!.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể đánh giá lại phân công đã kết chuyển.");
        assignment.DanhGia = input.Rating;
        assignment.SoTienPhuCap = actualAllowance;
        assignment.NgayDanhGia = DateTimeOffset.UtcNow;
        item.Version++;
        Audit(actor, item, "DANH_GIA_SU_KIEN", $"Đánh giá nhân viên #{assignment.MaNhanVien}: {input.Rating}; phụ cấp {(item.TinhPhuCapKhiNghiemThu ? "tính khi nghiệm thu" : actualAllowance.ToString())}.");
        return 0;
    });

    public async Task ChangeStatusAsync(EventActor actor, int id, long version, string status, string? reason = null)
    {
        // Auto-cancel on read removed per plan
        await Write(async () =>
        {
            actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        var operationStart = item.NgayBatDau;
        var operationEnd = item.NgayKetThuc;
        Require(HotelEventRules.CanTransition(item.TrangThai, status, operationStart, operationEnd, DateTimeOffset.UtcNow), "", "Chuyển trạng thái không hợp lệ hoặc chưa đến thời gian thực hiện.");
        if (item.TrangThai == "LEN_KE_HOACH" && status == "DANG_DIEN_RA")
            await EnsureReadyToStart(item);
        var oldStatus = item.TrangThai;
        if (status == "HUY")
        {
            Require(item.TrangThai == "LEN_KE_HOACH" && !item.PhanCongs.Any(p => p.CongThucTes.Count > 0), "", "Sự kiện đã thực hiện: dùng Dừng sự kiện và nghiệm thu công đã làm.");
            item.LyDoHuy = Text(reason, 500, "reason", "Lý do hủy");
            foreach (var assignment in item.PhanCongs)
            {
                Require(assignment.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể hủy sự kiện đã kết chuyển phụ cấp.");
                assignment.TrangThaiChiTra = "HUY_BO";
                assignment.SoTienPhuCap = 0;
            }
        }
        item.TrangThai = status;
        if (status == "CHO_NGHIEM_THU") item.KetThucThucTe = item.NgayKetThuc;
        item.Version++;
        Audit(actor, item, status == "HUY" ? "HUY_SU_KIEN" : "TRANG_THAI_SU_KIEN", $"Trạng thái: {oldStatus} → {status}.", item.LyDoHuy);
            return 0;
        });
    }

    public Task AcceptAsync(EventActor actor, int id, long version) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR hoặc Admin được nghiệm thu sự kiện.", true);
        var item = await Editable(actor, id, version);
        ReviewOpen(item);
        Require(!item.TinhTheoGio, "", "Sự kiện theo giờ: xem trước số tiền và chọn kỳ trả trước khi nghiệm thu.");
        Require(HotelEventRules.ReviewsReady(item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG"),
            item.TinhPhuCapKhiNghiemThu ? 9999999999999.99m : item.NganSachPhuCap), "",
            "Đánh giá toàn bộ nhân viên và bảo đảm tổng phụ cấp hợp lệ trước khi nghiệm thu.");
        Require(item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG").All(p => p.MaChiTiet == null && p.TrangThaiChiTra == "CHUA_CHI_TRA"), "", "Phụ cấp sự kiện đã kết chuyển hoặc bị hủy.");
        if (item.TinhPhuCapKhiNghiemThu)
        {
            var configs = await db.CauHinhKhoanLuongs.AsNoTracking().Where(k => k.LoaiKhoan == "THUONG").ToListAsync();
            decimal total = 0;
            foreach (var assignment in item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG"))
            {
                var amount = HotelEventRules.AcceptanceAmount(item, assignment, configs);
                Require(HotelEventRules.IsMoney(amount) && amount > 0 || assignment.DanhGia == "KHONG_THAM_GIA" && amount == 0,
                    "", $"Chưa có mức thưởng hợp lệ cho nhân viên #{assignment.MaNhanVien}.");
                Require(total <= 9999999999999.99m - amount, "", "Tổng phụ cấp vượt giới hạn lưu trữ.");
                assignment.SoTienPhuCap = amount;
                total += amount;
            }
            item.NganSachPhuCap = total;
        }
        item.TrangThai = "HOAN_THANH";
        item.MaNguoiNghiemThu = actor.EmployeeId; item.NgayNghiemThu = DateTimeOffset.UtcNow;
        item.Version++;
        Audit(actor, item, "NGHIEM_THU_SU_KIEN", $"Nghiệm thu {item.PhanCongs.Count} phân công; tổng phụ cấp {item.PhanCongs.Sum(p => p.SoTienPhuCap)}.");
        return 0;
    });

    private async Task<EventActor> CurrentActor(EventActor supplied)
    {
        var account = await db.TaiKhoans.AsNoTracking().Include(t => t.VaiTro).Include(t => t.NhanVien)
            .SingleOrDefaultAsync(t => t.MaTaiKhoan == supplied.AccountId && t.MaNhanVien == supplied.EmployeeId && t.TrangThai);
        Require(account?.NhanVien?.TrangThai == "DANG_LAM" && account.VaiTro != null, "", "Tài khoản không còn quyền truy cập sự kiện.", true);
        var actor = new EventActor(account!.MaNhanVien, account.MaTaiKhoan, account.VaiTro!.TenVaiTro, account.NhanVien!.HoTen);
        Require(actor.IsOffice || actor.IsManager, "", "Bạn không có quyền truy cập sự kiện.", true);
        return actor;
    }

    private static IQueryable<DuAn> Scoped(IQueryable<DuAn> events, EventActor actor) => actor.IsOffice ? events : events.Where(e => e.MaNguoiQuanLy == actor.EmployeeId);

    private async Task<DuAn> Editable(EventActor actor, int id, long version)
    {
        var item = await Scoped(Events, actor).SingleOrDefaultAsync(e => e.MaDuAn == id);
        Require(item != null, "", "Không tìm thấy sự kiện hoặc bạn không có quyền thao tác.", true);
        Require(item!.Version == version, "", "Sự kiện đã được cập nhật. Tải lại trang trước khi tiếp tục.");
        Require(item.TrangThai is not ("HOAN_THANH" or "HUY"), "", "Sự kiện đã nghiệm thu hoặc đã hủy; không thể thay đổi.");
        return item;
    }

    private static void StaffingOpen(DuAn item) => Require(item.TrangThai is "LEN_KE_HOACH" or "DANG_DIEN_RA", "", "Chỉ phân công khi đang lập kế hoạch hoặc đang diễn ra.");
    private static void ReviewOpen(DuAn item) => Require(item.TrangThai == "CHO_NGHIEM_THU" && (item.KetThucThucTe ?? item.NgayKetThuc) <= DateTimeOffset.UtcNow, "", "Chỉ đánh giá và nghiệm thu sau khi sự kiện kết thúc và ở trạng thái chờ nghiệm thu.");
    private static string Text(string? value, int max, string field, string label)
    {
        value = value?.Trim();
        Require(!string.IsNullOrEmpty(value) && value.Length <= max, field, $"{label} là bắt buộc, tối đa {max} ký tự.");
        return value!;
    }
    private static DateTimeOffset Utc(DateTime value, string field)
    {
        Require(value.Year is >= 1900 and <= 2100, field, "Nhập thời gian hợp lệ từ năm 1900 đến 2100.");
        return HotelEventRules.ToUtc(value);
    }
    private static void Require(bool condition, string field, string message, bool forbidden = false)
    {
        if (!condition) throw new EventOperationException(field, message, forbidden);
    }
    private void Audit(EventActor actor, DuAn item, string action, string message, string? reason = null, string? oldValues = null, string? newValues = null) => db.NhatKyQuanTris.Add(new NhatKyQuanTri
    {
        MaTaiKhoanThucHien = actor.AccountId, TenNguoiThucHien = actor.Name.Length <= 100 ? actor.Name : actor.Name[..100],
        TenTaiKhoanBiTacDong = $"SK-{item.MaDuAn}", HanhDong = action, NoiDung = message,
        GiaTriCu = oldValues, GiaTriMoi = newValues ?? $"version={item.Version}; trang_thai={item.TrangThai}", LyDo = reason
    });

    private async Task<T> Write<T>(Func<Task<T>> operation)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync();
                // One lock across all events prevents two events assigning the same employee concurrently.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
                var result = await operation();
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new EventOperationException("", "Sự kiện đã được cập nhật. Tải lại trang trước khi tiếp tục.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            db.ChangeTracker.Clear();
            var field = pg.TableName == "du_an" ? "Input.Name" : "Assignment.EmployeeId";
            throw new EventOperationException(field, "Tên sự kiện hoặc phân công đã tồn tại. Tải lại trang và kiểm tra dữ liệu.");
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
