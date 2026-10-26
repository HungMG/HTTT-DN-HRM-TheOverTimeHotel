using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.Data.Services;

public sealed partial class HotelEventService
{
    public async Task<IReadOnlyList<DiaDiemSuKien>> VenuesAsync() =>
        await db.DiaDiemSuKiens.AsNoTracking().Where(v => v.DangSuDung).OrderBy(v => v.TenDiaDiem).ToListAsync();

    public Task<int> SaveVenueAsync(EventActor actor, string name, int capacity) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin quản lý địa điểm.", true);
        name = Text(name, 255, "name", "Tên địa điểm");
        Require(capacity > 0, "capacity", "Sức chứa phải lớn hơn 0.");
        Require(!await db.DiaDiemSuKiens.AnyAsync(v => v.TenDiaDiem.ToLower() == name.ToLower()), "name", "Tên địa điểm đã có.");
        var venue = new DiaDiemSuKien { TenDiaDiem = name, SucChua = capacity };
        db.Add(venue);
        await db.SaveChangesAsync();
        db.NhatKyQuanTris.Add(new NhatKyQuanTri { MaTaiKhoanThucHien = actor.AccountId,
            TenNguoiThucHien = actor.Name.Length > 100 ? actor.Name[..100] : actor.Name,
            TenTaiKhoanBiTacDong = $"DIA_DIEM-{venue.MaDiaDiem}", HanhDong = "TAO_DIA_DIEM", NoiDung = $"Tạo địa điểm; sức chứa {capacity}." });
        return venue.MaDiaDiem;
    });

    public Task<CongBoLichTuan?> PublishedWeekAsync(DateOnly weekStart) => db.CongBoLichTuans.AsNoTracking()
        .SingleOrDefaultAsync(w => w.DauTuan == HourlyEventRules.WeekStart(weekStart));

    public Task PublishWeekAsync(EventActor actor, DateOnly weekStart, long version, string? reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin được công bố lịch tuần.", true);
        Require(weekStart == HourlyEventRules.WeekStart(weekStart), "weekStart", "Tuần phải bắt đầu từ thứ Hai.");
        Require(weekStart.Year is >= 1900 and <= 2100, "weekStart", "Tuần không hợp lệ.");
        var today = DateOnly.FromDateTime(HotelEventRules.Local(DateTimeOffset.UtcNow));
        if (today.DayOfWeek != DayOfWeek.Saturday || weekStart != HourlyEventRules.WeekStart(today).AddDays(7))
            reason = Text(reason, 500, "reason", "Lý do công bố bổ sung/ngoài thứ Bảy");
        else if (!string.IsNullOrWhiteSpace(reason)) reason = Text(reason, 500, "reason", "Lý do");
        var publication = await db.CongBoLichTuans.SingleOrDefaultAsync(w => w.DauTuan == weekStart);
        Require((publication?.Version ?? 0) == version, "", "Lịch đã được công bố/cập nhật; tải lại trước khi tiếp tục.");
        var shifts = await db.PhanCas.Include(s => s.NhanVien).ThenInclude(n => n!.HopDongs)
            .Where(s => s.NgayLamViec >= weekStart && s.NgayLamViec < weekStart.AddDays(7) && s.TrangThai != "DA_HUY").ToListAsync();
        Require(shifts.Count > 0, "", "Chưa có ca để công bố trong tuần này.");
        var employeeIds = shifts.Select(s => s.MaNhanVien).Distinct().ToArray();
        var adjacent = await db.PhanCas.AsNoTracking().Where(s => employeeIds.Contains(s.MaNhanVien) && s.TrangThai != "DA_HUY"
            && s.NgayLamViec >= weekStart.AddDays(-2) && s.NgayLamViec < weekStart.AddDays(9)).ToListAsync();
        foreach (var shift in shifts)
        {
            Require(shift.NhanVien != null && HotelEventRules.ContractCovers(shift.NhanVien, shift.BatDauDuKien, shift.KetThucDuKien),
                "", $"Ca #{shift.MaPhanCa}: nhân viên/hợp đồng không còn phù hợp.");
            Require(!await HasLeave(shift.MaNhanVien, shift.BatDauDuKien, shift.KetThucDuKien), "", $"Ca #{shift.MaPhanCa} trùng nghỉ đã duyệt.");
            var other = adjacent.Where(s => s.MaNhanVien == shift.MaNhanVien && s.MaPhanCa != shift.MaPhanCa)
                .Select(s => new ShiftSchedulingPolicy.ShiftWindow(s.BatDauDuKien,s.KetThucDuKien));
            var conflict = ShiftSchedulingPolicy.ValidateAssignment(other,shift.BatDauDuKien,shift.KetThucDuKien);
            Require(!conflict.HasConflict, "", $"Ca #{shift.MaPhanCa}: {conflict.ThongDiep}");
            shift.DaCongBo = true;
        }
        if (publication == null) { publication = new CongBoLichTuan { DauTuan = weekStart }; db.Add(publication); }
        else publication.Version++;
        publication.MaNguoiCongBo = actor.EmployeeId;
        publication.NgayCongBo = DateTimeOffset.UtcNow;
        publication.LyDo = reason?.Trim();
        db.NhatKyQuanTris.Add(new NhatKyQuanTri { MaTaiKhoanThucHien = actor.AccountId,
            TenNguoiThucHien = actor.Name.Length > 100 ? actor.Name[..100] : actor.Name, TenTaiKhoanBiTacDong = $"LICH-{weekStart:yyyy-MM-dd}",
            HanhDong = "CONG_BO_LICH_TUAN", NoiDung = $"Công bố {shifts.Count} ca; phiên bản {publication.Version}.", LyDo = reason });
        return 0;
    });

    public async Task<IReadOnlyList<PhanCa>> AvailableShiftsAsync(EventActor actor, int id)
    {
        var item = await GetAsync(actor, id);
        if (item == null) return [];
        var shifts = await db.PhanCas.AsNoTracking().Include(s => s.NhanVien).ThenInclude(n => n!.PhongBan)
            .Include(s => s.NhanVien).ThenInclude(n => n!.HopDongs).Include(s => s.CaLamViec).Include(s => s.ChamCong)
            .Where(s => s.DaCongBo && s.TrangThai != "DA_HUY" && s.TrangThai != "VANG_MAT"
                && s.BatDauDuKien < item.NgayKetThuc
                && s.KetThucDuKien > item.NgayBatDau)
            .OrderBy(s => s.BatDauDuKien).ThenBy(s => s.MaNhanVien).ToListAsync();
        return shifts.Where(s => HotelEventRules.GetRequiredDepartments(item.LoaiDuAn)
            .Contains(HotelEventRules.ResolveDepartmentCode(s.NhanVien) ?? "")).ToList();
    }

    public async Task<IReadOnlyList<KyLuong>> PaymentPeriodsAsync(EventActor actor)
    {
        actor = await CurrentActor(actor);
        if (!actor.IsOffice) return [];
        return await db.KyLuongs.AsNoTracking().Where(k => k.TrangThai == "MO" || k.TrangThai == "NHAP" || k.TrangThai == "TU_CHOI")
            .OrderBy(k => k.Nam).ThenBy(k => k.Thang).ToListAsync();
    }

    public Task<int> EnsurePaymentPeriodAsync(EventActor actor, int month, int year) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin tạo kỳ trả.", true);
        Require(month is >= 1 and <= 12 && year is >= 1900 and <= 2100, "", "Tháng/năm không hợp lệ.");
        var period = await db.KyLuongs.SingleOrDefaultAsync(k => k.Thang == month && k.Nam == year);
        if (period != null)
        {
            Require(period.TrangThai is "MO" or "NHAP" or "TU_CHOI", "", "Kỳ đã gửi duyệt/đã khóa; chọn kỳ mở khác.");
            return period.MaKyLuong;
        }
        period = new KyLuong { Thang = month, Nam = year, TuNgay = new(year,month,1), DenNgay = new(year,month,DateTime.DaysInMonth(year,month)), TrangThai = "MO" };
        db.Add(period); await db.SaveChangesAsync();
        db.NhatKyQuanTris.Add(new NhatKyQuanTri { MaTaiKhoanThucHien = actor.AccountId,
            TenNguoiThucHien = actor.Name.Length > 100 ? actor.Name[..100] : actor.Name, TenTaiKhoanBiTacDong = $"KY_LUONG-{period.MaKyLuong}",
            HanhDong = "TAO_KY_CHI_TRA", NoiDung = $"Tạo kỳ trả {month:D2}/{year}." });
        return period.MaKyLuong;
    });

    public Task ReconcileLegacyAsync(EventActor actor, int id, long version, int paymentPeriodId, string reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin đối soát tiền cũ.", true);
        var item = await Scoped(Events, actor).SingleOrDefaultAsync(e => e.MaDuAn == id);
        Require(item != null && item.Version == version, "", "Sự kiện không tồn tại hoặc đã thay đổi.");
        Require(!item!.TinhTheoGio && item.TrangThai == "HOAN_THANH", "", "Chỉ đối soát sự kiện chính sách cũ đã hoàn thành.");
        reason = Text(reason, 500, "reason", "Căn cứ đối soát số tiền đã chốt");
        var period = await db.KyLuongs.SingleOrDefaultAsync(k => k.MaKyLuong == paymentPeriodId);
        Require(period != null && period.TrangThai is "MO" or "NHAP" or "TU_CHOI", "paymentPeriodId", "Chọn kỳ còn sửa được.");
        var month = HourlyEventRules.WorkMonth(item.KetThucThucTe ?? item.NgayKetThuc);
        Require(new DateOnly(period!.Nam,period.Thang,1) >= month, "", "Kỳ trả không được trước tháng phát sinh.");
        Require(item.QuyetToans.Count == 0, "", "Đã đối soát, không tạo khoản trùng.");
        var assignments = item.PhanCongs.Where(p => p.MaChiTiet == null && p.TrangThaiChiTra == "CHUA_CHI_TRA"
            && p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.DanhGia is "HOAN_THANH" or "XUAT_SAC").ToList();
        Require(assignments.Count > 0, "", "Không có khoản cũ chưa kết chuyển để đối soát.");
        foreach (var group in assignments.GroupBy(p => p.MaNhanVien))
        {
            var amount = group.Sum(p => p.SoTienPhuCap);
            Require(HotelEventRules.IsMoney(amount), "", "Số tiền lịch sử không hợp lệ; cần kiểm tra chứng từ.");
            item.QuyetToans.Add(new QuyetToanSuKien { MaNhanVien = group.Key, ThangPhatSinh = month,
                MaKyChiTra = paymentPeriodId, SoTien = amount, SoPhut = 0,
                CanCuTinh = JsonSerializer.Serialize(new { Policy = "LEGACY_RECONCILED", Reason = reason,
                    Assignments = group.Select(p => new { p.MaPhanCong, p.SoTienPhuCap }).ToList() }),
                MaNguoiDuyet = actor.EmployeeId, NgayDuyet = DateTimeOffset.UtcNow });
        }
        item.Version++;
        Audit(actor,item,"DOI_SOAT_TIEN_SU_KIEN",$"Xác nhận {assignments.Count} khoản tiền cũ theo số đã lưu; kỳ trả #{paymentPeriodId}.",reason);
        return 0;
    });

    public Task SaveNeedAsync(EventActor actor, int id, EventNeedInput input) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin điều chỉnh nhu cầu sự kiện.", true);
        var item = await Editable(actor, id, input.Version);
        Require(item.TrangThai == "LEN_KE_HOACH", "", "Chỉ sửa nhu cầu trước khi bắt đầu sự kiện.");
        Require(HotelEventRules.GetRequiredDepartments(item.LoaiDuAn).Contains(input.Department), "Need.Department", "Phòng ban không phù hợp loại sự kiện.");
        var role = HotelEventRoleRules.ResolveRole(item.LoaiDuAn, input.Department, input.Role, null);
        Require(role != null, "Need.Role", "Chọn vai trò có trong danh mục.");
        Require(input.Count is >= 0 and <= 100000, "Need.Count", "Số người phải từ 0 đến 100.000.");
        Require(!item.NhuCaus.Any(n => n.MaNhuCau != input.NeedId && n.MaPhongBan == input.Department && n.VaiTro == role), "Need.Role", "Phòng ban/vai trò đã có nhu cầu; hãy sửa số người trên dòng hiện có.");
        var reason = Text(input.Reason, 500, "Need.Reason", "Lý do điều chỉnh nhu cầu");
        var need = input.NeedId == 0 ? new NhuCauSuKien { MaDuAn = id } : item.NhuCaus.SingleOrDefault(n => n.MaNhuCau == input.NeedId);
        Require(need != null, "", "Không tìm thấy nhu cầu.");
        var oldNeed = input.NeedId == 0 ? null : JsonSerializer.Serialize(new { need!.MaPhongBan, need.VaiTro, need.SoNguoi, need.LaMau });
        need!.MaPhongBan = input.Department;
        need.VaiTro = role!; need.SoNguoi = input.Count; need.LaMau = false;
        if (input.NeedId == 0) item.NhuCaus.Add(need);
        item.Version++;
        Audit(actor, item, "NHU_CAU_SU_KIEN", $"{need.MaPhongBan}: {need.VaiTro}; {need.SoNguoi} người cho toàn sự kiện.", reason, oldNeed);
        return 0;
    });

    public Task DeleteNeedAsync(EventActor actor, int id, int needId, long version, string reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin điều chỉnh nhu cầu.", true);
        var item = await Editable(actor, id, version);
        Require(item.TrangThai == "LEN_KE_HOACH", "", "Nhu cầu đã được sử dụng; không xóa sau khi bắt đầu.");
        var need = item.NhuCaus.SingleOrDefault(n => n.MaNhuCau == needId);
        Require(need != null, "", "Không tìm thấy nhu cầu.");
        var oldNeed = JsonSerializer.Serialize(new { need!.MaPhongBan, need.VaiTro, need.SoNguoi, need.LaMau });
        db.Remove(need); item.Version++;
        Audit(actor, item, "XOA_NHU_CAU_SU_KIEN", $"Xóa nhu cầu #{needId}.", Text(reason, 500, "reason", "Lý do"), oldNeed);
        return 0;
    });

    private async Task<bool> HasLeave(int employeeId, DateTimeOffset start, DateTimeOffset end)
    {
        var first = DateOnly.FromDateTime(HotelEventRules.Local(start));
        var last = DateOnly.FromDateTime(HotelEventRules.Local(end.AddTicks(-1)));
        return await db.DonTus.AnyAsync(d => d.MaNhanVien == employeeId && d.TrangThai == "DA_DUYET"
            && d.LoaiDon != null && d.LoaiDon.LaNghiPhep && d.TuNgay <= last && d.DenNgay >= first);
    }

    private async Task ValidateSource(DuAn item, PhanCongDuAn assignment, bool checkStaffing = true)
    {
        var shift = await db.PhanCas.Include(s => s.NhanVien).ThenInclude(n => n!.HopDongs)
            .SingleOrDefaultAsync(s => s.MaPhanCa == assignment.MaPhanCa);
        Require(shift != null && shift.DaCongBo && shift.TrangThai is not ("DA_HUY" or "VANG_MAT")
            && shift.MaNhanVien == assignment.MaNhanVien && shift.BatDauDuKien <= assignment.BatDau
            && shift.KetThucDuKien >= assignment.KetThuc, "", $"Phân công #{assignment.MaPhanCong}: ca nguồn chưa công bố hoặc đã thay đổi.");
        Require(shift!.NhanVien != null && HotelEventRules.ContractCovers(shift.NhanVien, assignment.BatDau, assignment.KetThuc), "", "Nhân viên/hợp đồng không còn phù hợp.");
        Require(!await HasLeave(assignment.MaNhanVien, assignment.BatDau, assignment.KetThuc), "", "Nhân viên trùng nghỉ đã duyệt.");
        Require(!await db.PhanCongDuAns.AnyAsync(p => p.MaNhanVien == assignment.MaNhanVien && p.MaPhanCong != assignment.MaPhanCong
            && p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.DuAn!.TrangThai != "HUY"
            && p.BatDau < assignment.KetThuc && p.KetThuc > assignment.BatDau), "", "Nhân viên đã có nhiệm vụ trùng giờ.");
        if (checkStaffing) await ValidateSourceCapacity(item, assignment, shift);
    }

    private async Task ValidateSourceCapacity(DuAn item, PhanCongDuAn assignment, PhanCa source)
    {
        var employee = source.NhanVien!;
        var baseline = await db.DinhBienCas.AsNoTracking().Where(r => r.MaCa == source.MaCa
            && r.MaChucVu == employee.MaChucVu && (r.MaPhongBan == null || r.MaPhongBan == employee.MaPhongBan)).ToListAsync();
        var overrides = await db.DinhBienNgays.AsNoTracking().Where(r => r.MaCa == source.MaCa
            && r.MaChucVu == employee.MaChucVu && r.Ngay == source.NgayLamViec
            && (r.MaPhongBan == null || r.MaPhongBan == employee.MaPhongBan)).ToListAsync();
        var required = overrides.Where(r => r.MaPhongBan == employee.MaPhongBan && r.MaPhongBan.HasValue).Select(r => (int?)r.SoNguoiToiThieu).FirstOrDefault()
            ?? baseline.Where(r => r.MaPhongBan == employee.MaPhongBan && r.MaPhongBan.HasValue).Select(r => (int?)r.SoNguoiToiThieu).FirstOrDefault()
            ?? overrides.Where(r => r.MaPhongBan == null).Select(r => (int?)r.SoNguoiToiThieu).FirstOrDefault()
            ?? baseline.Where(r => r.MaPhongBan == null).Select(r => (int?)r.SoNguoiToiThieu).FirstOrDefault() ?? 0;
        if (required == 0) return;
        var shifts = await db.PhanCas.AsNoTracking().Include(s => s.NhanVien).ThenInclude(n => n!.HopDongs)
            .Where(s => s.MaCa == source.MaCa && s.NgayLamViec == source.NgayLamViec && s.DaCongBo
                && s.TrangThai != "DA_HUY" && s.TrangThai != "VANG_MAT" && s.NhanVien!.MaPhongBan == employee.MaPhongBan
                && s.NhanVien.MaChucVu == employee.MaChucVu && s.NhanVien.TrangThai == "DANG_LAM").ToListAsync();
        shifts = shifts.Where(s => HotelEventRules.ContractCovers(s.NhanVien!, s.BatDauDuKien, s.KetThucDuKien)).ToList();
        var ids = shifts.Select(s => s.MaNhanVien).ToArray();
        var leaves = await db.DonTus.AsNoTracking().Where(d => ids.Contains(d.MaNhanVien) && d.TrangThai == "DA_DUYET"
            && d.LoaiDon!.LaNghiPhep && d.TuNgay <= source.NgayLamViec.AddDays(1) && d.DenNgay >= source.NgayLamViec).ToListAsync();
        var allocated = await db.PhanCongDuAns.AsNoTracking().Where(p => ids.Contains(p.MaNhanVien)
            && p.TrangThaiThamGia != "HUY_PHAN_CONG" && p.DuAn!.TrangThai != "HUY"
            && p.BatDau < assignment.KetThuc && p.KetThuc > assignment.BatDau && p.MaDuAn != item.MaDuAn).ToListAsync();
        allocated.AddRange(item.PhanCongs.Where(p => p.TrangThaiThamGia != "HUY_PHAN_CONG"));
        if (!allocated.Contains(assignment)) allocated.Add(assignment);
        var points = allocated.SelectMany(p => new[] { p.BatDau, p.KetThuc }).Concat(shifts.SelectMany(s => new[] { s.BatDauDuKien, s.KetThucDuKien }))
            .Append(assignment.BatDau).Where(t => t >= assignment.BatDau && t < assignment.KetThuc).Distinct();
        foreach (var point in points)
        {
            var remaining = shifts.Where(s => s.BatDauDuKien <= point && s.KetThucDuKien > point
                && !leaves.Any(d => d.MaNhanVien == s.MaNhanVien && d.TuNgay <= DateOnly.FromDateTime(HotelEventRules.Local(point))
                    && d.DenNgay >= DateOnly.FromDateTime(HotelEventRules.Local(point)))
                && !allocated.Any(p => p.MaNhanVien == s.MaNhanVien && p.BatDau <= point && p.KetThuc > point))
                .Select(s => s.MaNhanVien).Distinct().Count();
            Require(remaining >= required, "", $"Điều động làm thiếu định biên bộ phận nguồn lúc {HotelEventRules.Local(point):dd/MM HH:mm}: còn {remaining}, cần {required}.");
        }
    }

    public Task RecordWorkAsync(EventActor actor, int id, EventWorkInput input) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, input.Version);
        Require(item.TinhTheoGio && item.TrangThai is "DANG_DIEN_RA" or "CHO_NGHIEM_THU", "", "Chỉ ghi công cho sự kiện theo giờ đang thực hiện/chờ nghiệm thu.");
        var assignment = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == input.AssignmentId && p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY"));
        Require(assignment != null, "", "Không tìm thấy phân công đang hiệu lực.");
        Require(input.WorkId >= 0, "Work.WorkId", "Mã công không hợp lệ.");
        var editing = input.WorkId > 0;
        Require(!editing || actor.IsOffice, "", "Chỉ HR/Admin sửa công đã xác nhận.", true);
        Require(editing || assignment!.CongThucTes.Count == 0, "Work.WorkId", "Phân công đã có giờ tham gia. HR/Admin sửa khoảng giờ đã lưu.");
        var reason = editing ? Text(input.Reason, 500, "Work.Reason", "Lý do sửa công") : null;
        var work = editing ? assignment!.CongThucTes.SingleOrDefault(c => c.MaCong == input.WorkId) : new CongSuKien { MaPhanCong = assignment!.MaPhanCong };
        Require(work != null, "Work.WorkId", "Không tìm thấy đoạn công trong phân công này.");
        var oldWork = editing ? WorkSnapshot(work!, assignment!) : null;
        var start = Utc(input.Start, "Work.Start"); var end = Utc(input.End, "Work.End");
        Require(HourlyEventRules.CanRecordWork(item, assignment!, start, end, DateTimeOffset.UtcNow), "Work.End",
            "Công phải nằm trong sự kiện, phân công và ca nguồn đã công bố còn hiệu lực; không ghi công tương lai. Kiểm tra lại Lịch làm việc nếu dữ liệu nguồn thay đổi.");
        Require(!await db.CongSuKiens.AnyAsync(c => c.PhanCong!.MaNhanVien == assignment.MaNhanVien
            && c.MaCong != input.WorkId && c.BatDau < end && c.KetThuc > start), "", "Công thực tế trùng khoảng đã xác nhận của nhân viên, kể cả ở sự kiện khác.");
        work!.BatDau = start; work.KetThuc = end; work.MaNguoiXacNhan = actor.EmployeeId; work.NgayXacNhan = DateTimeOffset.UtcNow;
        if (!editing) assignment.CongThucTes.Add(work);
        assignment.DanhGia = "HOAN_THANH"; assignment.NgayDanhGia = DateTimeOffset.UtcNow;
        item.Version++;
        if (!editing) await db.SaveChangesAsync();
        Audit(actor, item, editing ? "SUA_CONG_SU_KIEN" : "XAC_NHAN_CONG_SU_KIEN",
            $"Nhân viên #{assignment.MaNhanVien}: {input.Start:g}–{input.End:g}.", reason, oldWork, WorkSnapshot(work, assignment));
        return 0;
    });

    public Task DeleteWorkAsync(EventActor actor, int id, int workId, long version, string reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin sửa công đã xác nhận.", true);
        var item = await Editable(actor, id, version);
        var assignment = item.PhanCongs.SingleOrDefault(p => p.CongThucTes.Any(c => c.MaCong == workId));
        Require(assignment != null, "", "Không tìm thấy công.");
        var work = assignment!.CongThucTes.Single(c => c.MaCong == workId);
        var oldWork = WorkSnapshot(work, assignment);
        assignment.CongThucTes.Remove(work); db.Remove(work);
        if (assignment.CongThucTes.Count == 0) { assignment.DanhGia = "CHUA_DANH_GIA"; assignment.NgayDanhGia = null; }
        item.Version++;
        Audit(actor, item, "SUA_CONG_SU_KIEN", $"Thu hồi công #{workId}; giữ dấu vết chỉnh sửa.", Text(reason, 500, "reason", "Lý do sửa công"), oldWork, "null");
        return 0;
    });

    private static string WorkSnapshot(CongSuKien work, PhanCongDuAn assignment) => JsonSerializer.Serialize(new
    {
        work.MaCong, work.MaPhanCong, assignment.MaNhanVien, work.BatDau, work.KetThuc,
        work.MaNguoiXacNhan, work.NgayXacNhan
    });

    public Task EndParticipationAsync(EventActor actor, int id, int assignmentId, long version, DateTime end, string reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version); StaffingOpen(item);
        var assignment = item.PhanCongs.SingleOrDefault(p => p.MaPhanCong == assignmentId && p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY"));
        Require(assignment != null, "", "Không tìm thấy phân công.");
        var instant = Utc(end, "end");
        Require(instant > assignment!.BatDau && instant <= assignment.KetThuc && instant <= DateTimeOffset.UtcNow,
            "end", "Kết thúc tham gia phải nằm trong phân công và không ở tương lai.");
        Require(assignment.CongThucTes.All(c => c.KetThuc <= instant), "", "Không thể cắt mất công đã xác nhận.");
        var previousEnd = assignment.KetThuc;
        assignment.KetThuc = instant;
        assignment.TrangThaiThamGia = "DA_KET_THUC";
        assignment.LyDoKetThuc = Text(reason, 500, "reason", "Lý do kết thúc/bàn giao");
        if (item.TrangThai == "DANG_DIEN_RA")
            Require(HotelEventRules.GetStaffingSummary(item, instant > DateTimeOffset.UtcNow ? instant : DateTimeOffset.UtcNow).IsFullyStaffed,
                "", "Thiếu nhân sự sau bàn giao; thêm người thay thế đúng vai trò trước.");
        item.Version++; Audit(actor, item, "KET_THUC_THAM_GIA", $"Kết thúc nhân viên #{assignment.MaNhanVien} lúc {end:g}; giữ công đã làm.", assignment.LyDoKetThuc,
            JsonSerializer.Serialize(new { assignment.MaPhanCong, assignment.MaNhanVien, assignment.BatDau, KetThuc = previousEnd }));
        return 0;
    });

    public Task StopAsync(EventActor actor, int id, long version, DateTime end, string reason) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        var item = await Editable(actor, id, version);
        Require(item.TrangThai == "DANG_DIEN_RA", "", "Chỉ dừng sự kiện đang thực hiện.");
        var instant = Utc(end, "end");
        var operationStart = item.NgayBatDau;
        Require(instant >= operationStart && instant <= DateTimeOffset.UtcNow && instant <= item.NgayKetThuc, "end", "Giờ dừng không hợp lệ.");
        Require(item.PhanCongs.SelectMany(p => p.CongThucTes).All(c => c.KetThuc <= instant), "", "Giờ dừng không được cắt mất công đã xác nhận.");
        item.KetThucThucTe = instant; item.LyDoDung = Text(reason, 500, "reason", "Lý do dừng");
        foreach (var p in item.PhanCongs.Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY")))
        {
            if (p.BatDau >= instant) p.TrangThaiThamGia = "HUY_PHAN_CONG";
            else { if (p.KetThuc > instant) p.KetThuc = instant; p.TrangThaiThamGia = "DA_KET_THUC"; }
            p.LyDoKetThuc = item.LyDoDung;
        }
        item.TrangThai = "CHO_NGHIEM_THU"; item.Version++;
        Audit(actor, item, "DUNG_SU_KIEN", $"Dừng lúc {end:g}; giữ công đã thực hiện.", item.LyDoDung);
        return 0;
    });

    public async Task<EventPayPreview> PreviewAsync(EventActor actor, int id)
    {
        var item = await GetAsync(actor, id);
        Require(item != null, "", "Không tìm thấy sự kiện.", true);
        return await BuildPreview(item!);
    }

    private async Task<EventPayPreview> BuildPreview(DuAn item)
    {
        var warnings = new List<string>(); var rows = new List<EventPayRow>();
        if (!item.TinhTheoGio) return new(item.Version, rows, ["Sự kiện theo chính sách cũ."], "");
        if (item.TrangThai == "HOAN_THANH")
        {
            rows.AddRange(item.QuyetToans.OrderBy(q => q.MaNhanVien).ThenBy(q => q.ThangPhatSinh).Select(q => new EventPayRow(
                q.MaNhanVien, item.PhanCongs.FirstOrDefault(p => p.MaNhanVien == q.MaNhanVien)?.NhanVien?.HoTen ?? $"#{q.MaNhanVien}",
                q.ThangPhatSinh, q.SoPhut, q.SoTien, JsonSerializer.Deserialize<List<EventPaySlice>>(q.CanCuTinh) ?? [])));
        }
        else
        {
            if (HotelEventRules.CountEffectiveStaff(item.PhanCongs) == 0)
                warnings.Add("Chưa có phân công hiệu lực từ Lịch làm việc để đối chiếu công sự kiện.");
            var recorded = item.PhanCongs.SelectMany(p => p.CongThucTes).ToList();
            var employees = item.PhanCongs.Select(p => p.MaNhanVien).Distinct().ToArray();
            var windowStart = recorded.Count > 0 ? recorded.Min(c => c.BatDau) : DateTimeOffset.MinValue;
            var windowEnd = recorded.Count > 0 ? recorded.Max(c => c.KetThuc) : DateTimeOffset.MinValue;
            var otherWork = recorded.Count > 0 ? await db.CongSuKiens.AsNoTracking()
                .Where(c => c.PhanCong!.MaDuAn != item.MaDuAn && employees.Contains(c.PhanCong.MaNhanVien)
                    && c.BatDau < windowEnd && c.KetThuc > windowStart)
                .Select(c => new { c.PhanCong!.MaNhanVien, c.BatDau, c.KetThuc }).ToListAsync() : [];
            foreach (var group in item.PhanCongs.Where(p => p.TrangThaiThamGia is not ("HUY_PHAN_CONG" or "DA_HUY") || p.CongThucTes.Count > 0).GroupBy(p => p.MaNhanVien).OrderBy(g => g.Key))
            {
                var name = group.First().NhanVien?.HoTen ?? $"#{group.Key}";
                if (group.SelectMany(p => p.CongThucTes).Any(c => otherWork.Any(o => o.MaNhanVien == group.Key
                    && HotelEventRules.Overlaps(c.BatDau, c.KetThuc, o.BatDau, o.KetThuc))))
                    warnings.Add($"{name}: công thực tế trùng với sự kiện khác; cần đối soát trước khi duyệt tiền.");
                if (group.Any(p => p.TrangThaiThamGia is "HUY_PHAN_CONG" or "DA_HUY"))
                    warnings.Add($"{name}: phân công đã hủy nhưng còn công thực tế; cần đối soát, không tự bỏ công đã làm.");
                if (group.Any(p => !p.NgayDanhGia.HasValue || p.DanhGia == "CHUA_DANH_GIA")) warnings.Add($"{name}: còn phân công chưa xác nhận công/vắng mặt.");
                if (group.Any(p => p.DanhGia != "KHONG_THAM_GIA" && p.CongThucTes.Count == 0)) warnings.Add($"{name}: thiếu công thực tế.");
                try
                {
                    foreach (var assignment in group)
                    {
                        foreach (var work in assignment.CongThucTes)
                        {
                            if (!HourlyEventRules.CanRecordWork(item, assignment, work.BatDau, work.KetThuc, DateTimeOffset.UtcNow))
                                warnings.Add($"{name}: ca/phân công/thời gian sự kiện không còn hợp lệ cho công #{work.MaCong}; cần kiểm tra lại nguồn phân công.");
                        }
                    }
                    var slices = group.SelectMany(p => p.CongThucTes.SelectMany(c => HourlyEventRules.Price(c.BatDau, c.KetThuc, p.NhanVien!.HopDongs))).ToList();
                    _ = HourlyEventRules.Amount(slices);
                    foreach (var month in slices.GroupBy(s => HourlyEventRules.WorkMonth(s.Start)).OrderBy(g => g.Key))
                        rows.Add(new(group.Key, name, month.Key, month.Sum(s => s.Minutes), HourlyEventRules.Amount(month), month.ToList()));
                }
                catch (ArgumentException e) { warnings.Add($"{name}: {e.Message}"); }
            }
        }
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { item.MaDuAn, item.Version, rows, warnings }))));
        return new(item.Version, rows, warnings, token);
    }

    public Task AcceptHourlyAsync(EventActor actor, int id, long version, int paymentPeriodId, string previewToken) => Write(async () =>
    {
        actor = await CurrentActor(actor);
        Require(actor.IsOffice, "", "Chỉ HR/Admin nghiệm thu.", true);
        var item = await Editable(actor, id, version); ReviewOpen(item);
        Require(item.TinhTheoGio, "", "Sự kiện không áp dụng chính sách theo giờ.");
        Require(HotelEventRules.CountEffectiveStaff(item.PhanCongs) > 0, "", "Sự kiện chưa có phân công thực hiện để nghiệm thu.");
        var preview = await BuildPreview(item);
        Require(preview.Warnings.Count == 0, "", string.Join(" ", preview.Warnings));
        Require(!string.IsNullOrEmpty(previewToken) && preview.Token == previewToken, "", "Căn cứ lương/công đã thay đổi. Xem lại bảng tiền trước khi nghiệm thu.");
        var period = await db.KyLuongs.SingleOrDefaultAsync(k => k.MaKyLuong == paymentPeriodId);
        Require(period != null && period.TrangThai is "MO" or "NHAP" or "TU_CHOI", "paymentPeriodId", "Chọn kỳ còn mở/nháp/từ chối; không nhận tiền vào kỳ đã gửi duyệt.");
        var periodMonth = new DateOnly(period!.Nam, period.Thang, 1);
        Require(preview.Rows.All(r => r.WorkMonth <= periodMonth), "paymentPeriodId", "Kỳ trả không được trước tháng phát sinh công.");
        Require(!await db.QuyetToanSuKiens.AnyAsync(q => q.MaDuAn == id), "", "Sự kiện đã có quyết toán; không tạo trùng.");
        foreach (var row in preview.Rows)
            item.QuyetToans.Add(new QuyetToanSuKien { MaNhanVien = row.EmployeeId, ThangPhatSinh = row.WorkMonth,
                MaKyChiTra = paymentPeriodId, SoPhut = row.Minutes, SoTien = row.Amount, CanCuTinh = JsonSerializer.Serialize(row.Basis),
                MaNguoiDuyet = actor.EmployeeId, NgayDuyet = DateTimeOffset.UtcNow });
        Require(HotelEventRules.IsMoney(preview.Total), "", "Tổng tiền vượt giới hạn lưu trữ.");
        item.NganSachPhuCap = preview.Total; item.TrangThai = "HOAN_THANH";
        item.MaNguoiNghiemThu = actor.EmployeeId; item.NgayNghiemThu = DateTimeOffset.UtcNow; item.Version++;
        Audit(actor, item, "NGHIEM_THU_SU_KIEN", $"Nghiệm thu {preview.Rows.Count} khoản theo giờ; tổng {preview.Total}; kỳ trả #{paymentPeriodId}.");
        return 0;
    });
}
