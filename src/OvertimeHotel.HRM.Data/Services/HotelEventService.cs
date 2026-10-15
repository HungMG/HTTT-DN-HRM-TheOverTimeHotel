using Microsoft.EntityFrameworkCore;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

public sealed class HotelEventService(AppDbContext db) : IHotelEventService
{
    private IQueryable<DuAn> Events => db.DuAns.Include(e => e.NguoiQuanLy)
        .Include(e => e.PhanCongs).ThenInclude(p => p.NhanVien).ThenInclude(n => n!.PhongBan)
        .Include(e => e.PhanCongs).ThenInclude(p => p.NhanVien).ThenInclude(n => n!.HopDongs).AsSplitQuery();

    public async Task<IReadOnlyList<DuAn>> ListAsync(EventActor actor, DateTimeOffset from, DateTimeOffset to)
    {
        actor = await CurrentActor(actor);
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        return await Scoped(Events.AsNoTracking(), actor)
            .Where(e => e.NgayBatDau < to && e.NgayKetThuc > from).OrderBy(e => e.NgayBatDau).ToListAsync();
    }

    public async Task<DuAn?> GetAsync(EventActor actor, int id)
    {
        actor = await CurrentActor(actor);
        return await Scoped(Events.AsNoTracking(), actor).SingleOrDefaultAsync(e => e.MaDuAn == id);
    }

    public async Task<IReadOnlyList<NhanVien>> ManagersAsync() => await db.NhanViens.AsNoTracking()
        .Where(n => n.TrangThai == "DANG_LAM" && n.TaiKhoan != null && n.TaiKhoan.TrangThai
            && n.TaiKhoan.VaiTro != null && (n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "MANAGER"
                || n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "HR" || n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "ADMIN"))
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
        var venue = Text(input.Venue, 255, "Input.Venue", "Địa điểm");
        Require(HotelEventRules.TypeLabels.ContainsKey(input.Type), "Input.Type", "Loại sự kiện không hợp lệ.");
        var start = Utc(input.Start, "Input.Start");
        var end = Utc(input.End, "Input.End");
        Require(input.Id != 0 || HotelEventRules.CanCreateOn(DateOnly.FromDateTime(input.Start), DateTimeOffset.UtcNow),
            "Input.Start", "Chỉ được tạo sự kiện từ hôm nay trở đi (giờ Việt Nam).");
        Require(end > start, "Input.End", "Kết thúc phải sau thời gian bắt đầu.");
        Require(HotelEventRules.IsMoney(input.Budget), "Input.Budget", "Ngân sách phải không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(input.ExpectedGuests >= 0, "Input.ExpectedGuests", "Số khách dự kiến không được âm.");
        Require(input.Description is null || input.Description.Length <= 10000, "Input.Description", "Mô tả tối đa 10.000 ký tự.");
        Require(!await db.DuAns.AnyAsync(e => e.MaDuAn != input.Id && e.TenDuAn == name), "Input.Name", "Tên sự kiện đã được sử dụng.");
        Require(await db.NhanViens.AnyAsync(n => n.MaNhanVien == input.ManagerId && n.TrangThai == "DANG_LAM"
            && n.TaiKhoan != null && n.TaiKhoan.TrangThai && n.TaiKhoan.VaiTro != null
            && (n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "MANAGER" || n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "HR" || n.TaiKhoan.VaiTro.TenVaiTro.ToUpper() == "ADMIN")),
            "Input.ManagerId", "Chọn quản lý có tài khoản quản lý đang hoạt động.");
        Require(item.PhanCongs.Count == 0 || (item.NgayBatDau == start && item.NgayKetThuc == end), "Input.Start",
            "Gỡ các phân công trước khi thay đổi thời gian sự kiện.");
        if (input.Id != 0 && input.Type != item.LoaiDuAn)
        {
            var newAllowed = HotelEventRules.GetRequiredDepartments(input.Type);
            var incompatible = item.PhanCongs
                .Where(p => {
                    var code = HotelEventRules.ResolveDepartmentCode(p.NhanVien);
                    return code == null || !newAllowed.Contains(code);
                })
                .ToList();
            Require(incompatible.Count == 0, "Input.Type",
                $"Không thể đổi loại sự kiện: có {incompatible.Count} nhân viên thuộc phòng ban không thuộc loại sự kiện mới. Vui lòng gỡ các phân công không phù hợp trước khi đổi.");
        }
        Require(item.PhanCongs.Sum(p => p.PhuCapDuKien) <= input.Budget, "Input.Budget", "Ngân sách thấp hơn tổng phụ cấp đã phân công.");
        var normalizedVenue = venue.ToLowerInvariant();
        Require(!await db.DuAns.AnyAsync(e => e.MaDuAn != input.Id && e.TrangThai != "HUY"
            && e.DiaDiem.Trim().ToLower() == normalizedVenue && e.NgayBatDau < end && e.NgayKetThuc > start),
            "Input.Venue", "Địa điểm đã có sự kiện khác trong khoảng thời gian này.");
        item.TenDuAn = name;
        item.LoaiDuAn = input.Type;
        item.DiaDiem = venue;
        item.NgayBatDau = start;
        item.NgayKetThuc = end;
        item.NganSachPhuCap = input.Budget;
        item.MaNguoiQuanLy = input.ManagerId;
        item.SoKhachDuKien = input.ExpectedGuests;
        item.MoTa = input.Description?.Trim();
        if (input.Id == 0) db.DuAns.Add(item);
        else item.Version++;
        await db.SaveChangesAsync(); // Identity is needed for the audit subject; still in the same transaction.
        Audit(actor, item, input.Id == 0 ? "TAO_SU_KIEN" : "SUA_SU_KIEN", input.Id == 0 ? "Tạo sự kiện." : "Cập nhật thông tin sự kiện.");
        return item.MaDuAn;
    });

    public Task AssignAsync(EventActor actor, int id, EventAssignmentInput input) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, input.Version);
        StaffingOpen(item);
        var role = Text(input.Role, 100, "Assignment.Role", "Vai trò trong sự kiện");
        Require(input.Task is null || input.Task.Length <= 255, "Assignment.Task", "Nhiệm vụ tối đa 255 ký tự.");
        var start = Utc(input.Start, "Assignment.Start");
        var end = Utc(input.End, "Assignment.End");
        Require(start >= item.NgayBatDau && start < end && end <= item.NgayKetThuc, "Assignment.End", "Thời gian tham gia phải nằm trong thời gian sự kiện.");
        Require(HotelEventRules.IsMoney(input.Allowance), "Assignment.Allowance", "Phụ cấp không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(!item.PhanCongs.Any(p => p.MaNhanVien == input.EmployeeId), "Assignment.EmployeeId", "Nhân viên đã được phân công trong sự kiện.");
        var employee = await db.NhanViens.Include(n => n.PhongBan).Include(n => n.HopDongs).SingleOrDefaultAsync(n => n.MaNhanVien == input.EmployeeId);
        Require(employee != null && HotelEventRules.ContractCovers(employee, start, end), "Assignment.EmployeeId", "Nhân viên phải đang làm việc và có hợp đồng hiệu lực suốt thời gian tham gia.");
        var allowedDepts = HotelEventRules.GetRequiredDepartments(item.LoaiDuAn);
        var empDept = HotelEventRules.ResolveDepartmentCode(employee);
        Require(empDept != null && allowedDepts.Contains(empDept), "Assignment.EmployeeId",
            $"Chỉ được chọn nhân viên thuộc các phòng ban quy định cho {HotelEventRules.TypeLabels.GetValueOrDefault(item.LoaiDuAn, "sự kiện")}.");
        Require(!await db.PhanCongDuAns.AnyAsync(p => p.MaNhanVien == input.EmployeeId && p.MaDuAn != id
            && p.DuAn!.TrangThai != "HUY" && p.BatDau < end && p.KetThuc > start), "Assignment.EmployeeId", "Nhân viên đã tham gia sự kiện khác trong khoảng thời gian này.");
        Require(item.PhanCongs.Sum(p => p.PhuCapDuKien) + input.Allowance <= item.NganSachPhuCap, "Assignment.Allowance", "Tổng phụ cấp dự kiến vượt ngân sách sự kiện.");
        item.PhanCongs.Add(new PhanCongDuAn
        {
            MaNhanVien = input.EmployeeId, VaiTroDuAn = role, NhiemVuCuThe = input.Task?.Trim(),
            BatDau = start, KetThuc = end, PhuCapDuKien = input.Allowance
        });
        item.Version++;
        Audit(actor, item, "PHAN_CONG_SU_KIEN", $"Phân công nhân viên #{input.EmployeeId}.");
        return 0;
    });

    public Task RemoveAssignmentAsync(EventActor actor, int id, int assignmentId, long version) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        StaffingOpen(item);
        var assignment = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == assignmentId);
        Require(assignment != null, "", "Không tìm thấy phân công.");
        Require(assignment!.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể gỡ phân công đã kết chuyển.");
        if (item.TrangThai == "DANG_DIEN_RA")
        {
            var deptCode = HotelEventRules.ResolveDepartmentCode(assignment.NhanVien);
            if (deptCode != null && HotelEventRules.GetRequiredDepartments(item.LoaiDuAn).Contains(deptCode))
            {
                var countInDept = item.PhanCongs.Count(p => HotelEventRules.ResolveDepartmentCode(p.NhanVien) == deptCode);
                Require(countInDept > 1, "",
                    $"Không thể gỡ nhân viên cuối cùng của phòng ban {HotelEventRules.DepartmentLabels.GetValueOrDefault(deptCode, deptCode)} khi sự kiện đang diễn ra. Phải thêm người thay thế trước.");
            }
        }
        db.PhanCongDuAns.Remove(assignment);
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
        Require(HotelEventRules.IsMoney(input.ActualAllowance), "Review.ActualAllowance", "Phụ cấp thực tế không âm, tối đa 13 chữ số và 2 số thập phân.");
        Require(input.Rating != "KHONG_THAM_GIA" || input.ActualAllowance == 0, "Review.ActualAllowance", "Nhân viên không tham gia phải có phụ cấp bằng 0.");
        Require(item.PhanCongs.Where(p => p.MaPhanCong != input.AssignmentId).Sum(p => p.SoTienPhuCap) + input.ActualAllowance <= item.NganSachPhuCap,
            "Review.ActualAllowance", "Tổng phụ cấp thực tế vượt ngân sách sự kiện.");
        Require(assignment!.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể đánh giá lại phân công đã kết chuyển.");
        assignment.DanhGia = input.Rating;
        assignment.SoTienPhuCap = input.ActualAllowance;
        assignment.NgayDanhGia = DateTimeOffset.UtcNow;
        item.Version++;
        Audit(actor, item, "DANH_GIA_SU_KIEN", $"Đánh giá nhân viên #{assignment.MaNhanVien}: {input.Rating}; phụ cấp {input.ActualAllowance}.");
        return 0;
    });

    public Task ChangeStatusAsync(EventActor actor, int id, long version, string status, string? reason = null) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        Require(HotelEventRules.CanTransition(item.TrangThai, status, item.NgayBatDau, item.NgayKetThuc, DateTimeOffset.UtcNow), "", "Chuyển trạng thái không hợp lệ hoặc chưa đến thời gian thực hiện.");
        if (item.TrangThai == "LEN_KE_HOACH" && status == "DANG_DIEN_RA")
        {
            var summary = HotelEventRules.GetStaffingSummary(item.LoaiDuAn, item.PhanCongs);
            Require(summary.IsFullyStaffed, "",
                $"Chưa thể bắt đầu sự kiện vì còn thiếu nhân sự phòng ban: {string.Join(", ", summary.MissingDepartmentNames)}.");
        }
        var oldStatus = item.TrangThai;
        if (status == "HUY")
        {
            item.LyDoHuy = Text(reason, 500, "reason", "Lý do hủy");
            foreach (var assignment in item.PhanCongs)
            {
                Require(assignment.MaChiTiet == null && assignment.TrangThaiChiTra == "CHUA_CHI_TRA", "", "Không thể hủy sự kiện đã kết chuyển phụ cấp.");
                assignment.TrangThaiChiTra = "HUY_BO";
                assignment.SoTienPhuCap = 0;
            }
        }
        item.TrangThai = status;
        item.Version++;
        Audit(actor, item, status == "HUY" ? "HUY_SU_KIEN" : "TRANG_THAI_SU_KIEN", $"Trạng thái: {oldStatus} → {status}.", item.LyDoHuy);
        return 0;
    });

    public Task AcceptAsync(EventActor actor, int id, long version) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR hoặc Admin được nghiệm thu sự kiện.", true);
        var item = await Editable(actor, id, version);
        ReviewOpen(item);
        Require(HotelEventRules.ReviewsReady(item.PhanCongs, item.NganSachPhuCap), "", "Đánh giá toàn bộ nhân viên và bảo đảm tổng phụ cấp không vượt ngân sách trước khi nghiệm thu.");
        Require(item.PhanCongs.All(p => p.MaChiTiet == null && p.TrangThaiChiTra == "CHUA_CHI_TRA"), "", "Phụ cấp sự kiện đã kết chuyển hoặc bị hủy.");
        item.TrangThai = "HOAN_THANH";
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
    private static void ReviewOpen(DuAn item) => Require(item.TrangThai == "CHO_NGHIEM_THU" && item.NgayKetThuc <= DateTimeOffset.UtcNow, "", "Chỉ đánh giá và nghiệm thu sau khi sự kiện kết thúc và ở trạng thái chờ nghiệm thu.");
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
    private void Audit(EventActor actor, DuAn item, string action, string message, string? reason = null) => db.NhatKyQuanTris.Add(new NhatKyQuanTri
    {
        MaTaiKhoanThucHien = actor.AccountId, TenNguoiThucHien = actor.Name.Length <= 100 ? actor.Name : actor.Name[..100],
        TenTaiKhoanBiTacDong = $"SK-{item.MaDuAn}", HanhDong = action, NoiDung = message,
        GiaTriMoi = $"version={item.Version}; trang_thai={item.TrangThai}", LyDo = reason
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
