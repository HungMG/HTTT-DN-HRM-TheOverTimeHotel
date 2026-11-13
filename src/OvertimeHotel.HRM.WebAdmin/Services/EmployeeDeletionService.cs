using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Constants;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed record EmployeeDeletionImpact(string Label, int Count);

public sealed class EmployeeDeletionPreview
{
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = "";
    public List<string> Blockers { get; } = [];
    public List<EmployeeDeletionImpact> Impacts { get; } = [];
    public bool CanDelete => Blockers.Count == 0;
    public string ConfirmationToken { get; set; } = "";
}

public sealed record EmployeeDeletionResult(bool Deleted, string Message);

// Preview and confirmation share the same database checks. No cached employee lists here.
public sealed class EmployeeDeletionService(AppDbContext db, IDataProtectionProvider protection)
{
    private readonly ITimeLimitedDataProtector _protector = protection
        .CreateProtector("HRM.EmployeePermanentDeletion.v3").ToTimeLimitedDataProtector();

    public static string CreateConfirmationToken(IDataProtectionProvider protection, int id) => protection
        .CreateProtector("HRM.EmployeePermanentDeletion.v3").ToTimeLimitedDataProtector()
        .Protect(id.ToString(System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromHours(12));

    public async Task<EmployeeDeletionPreview?> PreviewAsync(int id)
    {
        var plan = await BuildPlanAsync(id);
        if (plan == null) return null;
        if (plan.Preview.CanDelete)
            plan.Preview.ConfirmationToken = CreateConfirmationToken(protection, id);
        return plan.Preview;
    }

    public async Task<EmployeeDeletionResult> DeleteAsync(int id, string? confirmationToken, bool confirmed)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(confirmationToken))
            return new(false, "Vui lòng mở cảnh báo và xác nhận xóa vĩnh viễn nhân viên.");

        string expected;
        try { expected = _protector.Unprotect(confirmationToken); }
        catch (CryptographicException) { return new(false, "Xác nhận không hợp lệ hoặc đã hết hạn. Hãy mở lại cảnh báo."); }

        // Npgsql is configured with retry-on-failure; the strategy must own the whole transaction.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            try { return await DeleteConfirmedAsync(id, expected); }
            catch { db.ChangeTracker.Clear(); throw; }
        });
    }

    private async Task<EmployeeDeletionResult> DeleteConfirmedAsync(int id, string expected)
    {
        // Keep the checks, appendix removal and cascaded deletion in one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (db.Database.IsNpgsql())
        {
            // Match salary/payroll writers' lock order before taking an employee row lock.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(2026101509)");
            // FK inserts and edits must wait while we recheck and remove this employee.
            await db.NhanViens.FromSqlInterpolated($"SELECT * FROM nhan_vien WHERE ma_nhan_vien = {id} FOR UPDATE")
                .AsNoTracking().ToListAsync();
        }
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (await db.HopDongs.AnyAsync(contract => contract.MaNhanVien == id
            && contract.TrangThai != "DA_CHAM_DUT" && contract.TrangThai != "CHAM_DUT"
            && (contract.NgayKetThuc == null || contract.NgayKetThuc >= today)))
            return new(false, "Cần chấm dứt hợp đồng trước khi xóa vĩnh viễn nhân viên. Nhân viên còn hợp đồng hiệu lực hoặc chưa đến ngày bắt đầu.");

        var plan = await BuildPlanAsync(id);
        if (plan == null) return new(false, "Hồ sơ nhân viên không còn tồn tại.");
        if (!plan.Preview.CanDelete) return new(false, string.Join(" ", plan.Preview.Blockers));
        if (expected != id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            return new(false, "Xác nhận không khớp nhân viên. Hãy tải lại trang trước khi xóa.");

        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('hrm.purge_employee_id', {id.ToString(System.Globalization.CultureInfo.InvariantCulture)}, true)");
        foreach (var layer in plan.DeleteLayers)
        {
            foreach (var entity in layer) db.Entry(entity).State = EntityState.Deleted;
            await db.SaveChangesAsync();
        }
        // Nullable actor references keep the other employee's document, removing only its actor link.
        await db.DuAns.Where(x => x.MaNguoiNghiemThu == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.MaNguoiNghiemThu, (int?)null));
        await db.NganSachLuongs.Where(x => x.NguoiTao == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.NguoiTao, (int?)null).SetProperty(x => x.Version, x => x.Version + 1));
        await db.NganSachLuongs.Where(x => x.NguoiDuyet == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.NguoiDuyet, (int?)null).SetProperty(x => x.Version, x => x.Version + 1));
        db.Entry(plan.Employee).State = EntityState.Deleted;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true, $"Đã xóa vĩnh viễn nhân viên “{plan.Employee.HoTen}” và dữ liệu liên quan đã xác nhận.");
    }

    private sealed record DeletionPlan(NhanVien Employee, EmployeeDeletionPreview Preview, string Fingerprint, List<List<object>> DeleteLayers);

    private async Task<DeletionPlan?> BuildPlanAsync(int id)
    {
        var employee = await db.NhanViens.AsNoTracking().AsSplitQuery()
            .Include(e => e.HopDongs).ThenInclude(c => c.PhuLucLuongs)
            .Include(e => e.TaiKhoan)
            .Include(e => e.PhanCas).ThenInclude(s => s.ChamCong)
            .Include(e => e.DonTus)
            .Include(e => e.YeuCauTaiKhoanCuaNhanVien)
            .SingleOrDefaultAsync(e => e.MaNhanVien == id);
        if (employee == null) return null;

        var preview = new EmployeeDeletionPreview { EmployeeId = id, EmployeeName = employee.HoTen };
        var shiftIds = employee.PhanCas.Select(x => x.MaPhanCa).ToArray();
        var projects = await db.DuAns.AsNoTracking().Where(x => x.MaNguoiQuanLy == id).ToListAsync();
        var projectIds = projects.Select(x => x.MaDuAn).ToArray();
        var assignments = await db.PhanCongDuAns.AsNoTracking().Where(x => x.MaNhanVien == id || projectIds.Contains(x.MaDuAn)
            || (x.MaPhanCa != null && shiftIds.Contains(x.MaPhanCa.Value))).ToListAsync();
        var assignmentIds = assignments.Select(x => x.MaPhanCong).ToArray();
        var work = await db.CongSuKiens.AsNoTracking().Where(x => assignmentIds.Contains(x.MaPhanCong) || x.MaNguoiXacNhan == id).ToListAsync();
        var settlements = await db.QuyetToanSuKiens.AsNoTracking().Where(x => x.MaNhanVien == id || x.MaNguoiDuyet == id || projectIds.Contains(x.MaDuAn)).ToListAsync();
        var swaps = await db.YeuCauDoiCas.AsNoTracking().Where(x => x.MaNhanVienGui == id || x.MaNhanVienNhan == id
            || shiftIds.Contains(x.MaPhanCaGui) || shiftIds.Contains(x.MaPhanCaNhan)).ToListAsync();
        var requests = await db.YeuCauTaiKhoans.AsNoTracking().Where(x => x.MaNhanVien == id || x.MaNguoiGui == id).ToListAsync();
        var explanations = await db.GiaiTrinhChamCongs.AsNoTracking().Where(x => x.MaNhanVien == id || shiftIds.Contains(x.MaPhanCa)).ToListAsync();
        var payroll = await db.PhieuLuongs.AsNoTracking().Include(x => x.ChiTietPhieuLuongs).Where(x => x.MaNhanVien == id).ToListAsync();
        var weeks = await db.CongBoLichTuans.AsNoTracking().Where(x => x.MaNguoiCongBo == id).ToListAsync();
        var needs = await db.NhuCauSuKiens.AsNoTracking().Where(x => projectIds.Contains(x.MaDuAn)).ToListAsync();
        var approvedForms = await db.DonTus.AsNoTracking().Where(x => x.MaNguoiDuyet == id && x.MaNhanVien != id).Select(x => x.MaDon).ToListAsync();
        var deletedSwapIds = swaps.Select(x => x.MaYeuCau).ToArray();
        var approvedSwaps = await db.YeuCauDoiCas.AsNoTracking().Where(x => x.MaNguoiDuyet == id && !deletedSwapIds.Contains(x.MaYeuCau)).Select(x => x.MaYeuCau).ToListAsync();
        var reviewedExplanations = await db.GiaiTrinhChamCongs.AsNoTracking().Where(x => x.MaNhanVien != id && (x.MaQuanLyXacMinh == id || x.MaHrXacNhan == id)).Select(x => x.MaGiaiTrinh).ToListAsync();
        var budgets = await db.NganSachLuongs.AsNoTracking().Where(x => x.NguoiTao == id || x.NguoiDuyet == id).Select(x => x.MaNganSach).ToListAsync();
        var acceptedProjects = await db.DuAns.AsNoTracking().Where(x => x.MaNguoiNghiemThu == id).Select(x => x.MaDuAn).ToListAsync();
        var layers = new List<List<object>>
        {
            work.Cast<object>().ToList(), settlements.Cast<object>().ToList(), swaps.Cast<object>().ToList(),
            requests.Cast<object>().ToList(), explanations.Cast<object>().ToList(),
            employee.HopDongs.SelectMany(x => x.PhuLucLuongs).Cast<object>().ToList(),
            payroll.Cast<object>().ToList(), assignments.Cast<object>().ToList(), needs.Cast<object>().ToList(),
            projects.Cast<object>().ToList(), weeks.Cast<object>().ToList()
        };
        // Include identities as well as counts: replacing a record also invalidates the preview.
        var identities = new List<string>();
        void Impact(string label, IEnumerable<long> keys)
        {
            var ids = keys.Order().ToArray();
            preview.Impacts.Add(new(label, ids.Length));
            identities.Add(label + ":" + string.Join(',', ids));
        }
        Impact("Hợp đồng (kể cả còn hiệu lực)", employee.HopDongs.Select(x => (long)x.MaHopDong));
        Impact("Phụ lục lương", employee.HopDongs.SelectMany(x => x.PhuLucLuongs).Select(x => x.MaPhuLuc));
        Impact("Tài khoản ứng dụng", employee.TaiKhoan == null ? [] : [(long)employee.TaiKhoan.MaTaiKhoan]);
        Impact("Phân ca", employee.PhanCas.Select(x => (long)x.MaPhanCa));
        Impact("Chấm công", employee.PhanCas.Where(x => x.ChamCong != null).Select(x => (long)x.ChamCong!.MaChamCong));
        Impact("Đơn từ của nhân viên", employee.DonTus.Select(x => (long)x.MaDon));
        Impact("Giải trình chấm công", explanations.Select(x => (long)x.MaGiaiTrinh));
        Impact("Yêu cầu cấp tài khoản do nhân viên gửi hoặc nhận", requests.Select(x => (long)x.MaYeuCau));
        Impact("Phiếu lương (kèm toàn bộ chi tiết lương, thuế, bảo hiểm)", payroll.Select(x => (long)x.MaPhieuLuong));
        Impact("Yêu cầu đổi ca liên quan", swaps.Select(x => (long)x.MaYeuCau));
        Impact("Sự kiện nhân viên quản lý: xóa toàn bộ, kể cả dữ liệu người tham gia khác", projects.Select(x => (long)x.MaDuAn));
        Impact("Phân công sự kiện bị xóa", assignments.Select(x => (long)x.MaPhanCong));
        Impact("Công sự kiện đã tham gia hoặc xác nhận", work.Select(x => (long)x.MaCong));
        Impact("Quyết toán sự kiện đã nhận hoặc duyệt", settlements.Select(x => (long)x.MaQuyetToan));
        Impact("Nhu cầu nhân sự của sự kiện bị xóa", needs.Select(x => (long)x.MaNhuCau));
        preview.Impacts.Add(new("Công bố lịch tuần bị xóa", weeks.Count));
        preview.Impacts.Add(new("Đơn người khác: giữ đơn, gỡ người duyệt", approvedForms.Count));
        preview.Impacts.Add(new("Đổi ca người khác: giữ yêu cầu, gỡ người duyệt", approvedSwaps.Count));
        preview.Impacts.Add(new("Giải trình người khác: giữ giải trình, gỡ người xác nhận", reviewedExplanations.Count));
        preview.Impacts.Add(new("Ngân sách: giữ bản ghi, gỡ người lập/duyệt", budgets.Count));
        preview.Impacts.Add(new("Sự kiện khác: giữ sự kiện, gỡ người nghiệm thu", acceptedProjects.Count));
        identities.Add(JsonSerializer.Serialize(new { approvedForms, approvedSwaps, reviewedExplanations, budgets, acceptedProjects,
            weeks = weeks.Select(x => x.DauTuan), payrollDetails = payroll.SelectMany(x => x.ChiTietPhieuLuongs).Select(x => x.MaChiTiet) }));
        var snapshot = JsonSerializer.Serialize(new
        {
            id, employee.TrangThai, employee.NgayThoiViec, employee.Ho, employee.Ten, identities,
            contracts = employee.HopDongs.OrderBy(c => c.MaHopDong)
                .Select(c => new { c.MaHopDong, c.SoHopDong, c.TrangThai, c.NgayBatDau, c.NgayKetThuc })
        });
        return new(employee, preview, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))), layers);
    }
}
