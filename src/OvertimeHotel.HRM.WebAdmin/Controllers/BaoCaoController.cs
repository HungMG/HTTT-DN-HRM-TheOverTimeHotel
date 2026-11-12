using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.WebAdmin.Models;
using OvertimeHotel.HRM.WebAdmin.Security;
using OvertimeHotel.HRM.WebAdmin.Services;

namespace OvertimeHotel.HRM.WebAdmin.Controllers;

/// <summary>
/// Phân hệ Báo cáo &amp; Thống kê Quản trị (Phân hệ 10.0 — TSK-24), gồm sáu báo cáo 10.1 → 10.6.
///
/// <para>
/// Mọi công thức nghiệp vụ (tỷ lệ thôi việc, xếp nhóm thâm niên, chuẩn hóa học vấn, đối soát
/// định mức, chặn chia 0) đều nằm ở <see cref="ReportPolicy"/>; phần dựng số liệu nằm ở
/// <see cref="BaoCaoReportService"/> — controller chỉ lo định tuyến, kiểm quyền và xuất CSV.
/// Trợ lý AI dùng chung đúng service đó nên hai nơi không thể lệch số liệu.
/// </para>
///
/// <para>
/// <b>Phạm vi dữ liệu do máy chủ quyết định, không do URL:</b> Quản lý bị
/// <see cref="ReportPolicy.ResolveScope"/> ghép cứng vào bộ phận của mình, nên tham số
/// <c>phongBan</c> trên địa chỉ không thể mở rộng tầm nhìn. Thiếu claim phòng ban thì bị từ chối
/// thẳng (fail-closed).
/// </para>
/// </summary>
[Authorize(Policy = PermissionCodes.ReportView)]
public class BaoCaoController : Controller
{
    /// <summary>Múi giờ Việt Nam, dùng để hiển thị mốc thời gian và quy đổi ngày sự kiện.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly BaoCaoReportService _reportService;
    private readonly ILogger<BaoCaoController> _logger;

    public BaoCaoController(BaoCaoReportService reportService, ILogger<BaoCaoController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Trang báo cáo. Gọi thường thì trả cả trang (header + bộ lọc + vùng dữ liệu); gọi bằng
    /// AJAX thì chỉ trả vùng dữ liệu <c>_BaoCaoBoard</c> để đổi kỳ / phòng ban / tab tại chỗ,
    /// không nạp lại toàn trang theo quy tắc "Zero Full-Page Reload" của coding-style mục 4.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            // Vai trò không có quyền, hoặc Quản lý thiếu claim phòng ban.
            return Forbid();
        }

        var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);

        if (IsAjaxRequest())
        {
            // Gửi kèm nhãn kỳ và phạm vi để thanh trạng thái cập nhật đúng dù không nạp lại trang.
            Response.Headers["X-Report-Period"] = Uri.EscapeDataString(model.NhanKy);
            Response.Headers["X-Report-Scope"] = Uri.EscapeDataString(model.PhamViLabel);
            return PartialView("_BaoCaoBoard", model);
        }

        return View(model);
    }

    /// <summary>
    /// Xuất CSV cho đúng tab đang chọn. Dùng UTF-8 kèm BOM để Excel đọc đúng tiếng Việt
    /// (theo mẫu <c>ExportBankTransferFileAsync</c> của phân hệ lương).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Export(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (BaoCaoReportService.NormalizeTab(tab) == "quy-luong" && !ReportPolicy.CanViewPayrollFundReport(role))
        {
            return Forbid();
        }

        try
        {
            var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);
            if (!model.IsDatabaseOnline)
            {
                _logger.LogWarning("Không thể xuất CSV vì dữ liệu CSDL chưa tải được hoặc bị ngắt kết nối.");
                TempData["Error"] = "Không thể xuất tệp báo cáo vì kết nối dữ liệu gián đoạn. Vui lòng thử lại sau.";
                return StatusCode(503, "Dữ liệu báo cáo chưa sẵn sàng.");
            }

            var (fileName, content) = BuildCsv(model);
            return File(content, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xuất CSV báo cáo thống kê.");
            TempData["Error"] = "Không thể xuất tệp báo cáo. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index), new { ky, nam, thang, quy, phongBan, tab });
        }
    }

    /// <summary>
    /// Xuất bảng tính .xlsx có kiểu ô số/văn bản, không phụ thuộc thư viện ngoài.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (BaoCaoReportService.NormalizeTab(tab) == "quy-luong" && !ReportPolicy.CanViewPayrollFundReport(role))
        {
            return Forbid();
        }

        try
        {
            var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);
            if (!model.IsDatabaseOnline)
            {
                return StatusCode(503, "Dữ liệu báo cáo chưa sẵn sàng.");
            }

            var (fileName, content) = BuildExcelFile(model);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xuất Excel báo cáo thống kê.");
            TempData["Error"] = "Không thể xuất tệp Excel.";
            return RedirectToAction(nameof(Index), new { ky, nam, thang, quy, phongBan, tab });
        }
    }

    /// <summary>
    /// Xem dữ liệu nguồn phục vụ đối chứng từ chỉ tiêu (Task 9).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(
        string type,
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var details = await _reportService.BuildChiTietAsync(scope, role, type, ky, nam, thang, quy, phongBan);
        return PartialView("_BaoCaoDetails", details);
    }

    /// <summary>
    /// Trang in / lưu PDF khổ A4 tối ưu (Task 12).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Print(
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var scope = ResolveScope();
        if (!scope.IsAllowed)
        {
            return Forbid();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (BaoCaoReportService.NormalizeTab(tab) == "quy-luong" && !ReportPolicy.CanViewPayrollFundReport(role)) return Forbid();
        var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);
        if (!model.IsDatabaseOnline) return StatusCode(503, "Dữ liệu báo cáo chưa sẵn sàng.");
        return View("Print", model);
    }

    /// <summary>
    /// Dựng số liệu cho kỳ đang chọn. Chỉ lấy vai trò từ phiên rồi chuyển tiếp sang service
    /// dùng chung — công thức và truy vấn nằm trọn ở <see cref="BaoCaoReportService"/>.
    /// </summary>
    private Task<BaoCaoViewModel> BuildAsync(
        ReportPolicy.BaoCaoScope scope,
        string? ky,
        int? nam,
        int? thang,
        int? quy,
        int? phongBan,
        string? tab)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return _reportService.BuildAsync(scope, role, ky, nam, thang, quy, phongBan, tab);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  XUẤT CSV
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Dựng nội dung CSV cho tab đang chọn, kèm BOM để Excel nhận đúng UTF-8.</summary>
    private static (string FileName, byte[] Content) BuildCsv(BaoCaoViewModel model)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');

        sb.AppendLine($"BÁO CÁO THỐNG KÊ NHÂN SỰ — {model.NhanKy}");
        sb.AppendLine($"Phạm vi: {model.PhamViLabel}");
        sb.AppendLine($"Người xuất: {model.RoleLabel} · Lúc {model.GeneratedAt.ToOffset(VietnamOffset):dd/MM/yyyy HH:mm}");
        sb.AppendLine();

        switch (model.Tab)
        {
            case "co-cau":
                AppendCoCau(sb, model);
                break;
            case "hoc-van":
                AppendHocVan(sb, model.HocVanThamNien);
                break;
            case "quy-luong":
                AppendQuyLuong(sb, model.QuyLuong);
                break;
            case "chuyen-can":
                AppendChuyenCan(sb, model.ChuyenCan);
                break;
            case "su-kien":
                AppendSuKien(sb, model.SuKien);
                break;
            default:
                AppendBienDong(sb, model.BienDong);
                break;
        }

        if (model.SoSanhCungKy.Count > 0)
        {
            sb.AppendLine(); sb.AppendLine($"SO SÁNH VỚI NĂM TRƯỚC,{Esc(model.CungKyLabel)}");
            sb.AppendLine("Nội dung,Đơn vị,Thời gian đã chọn,Cùng thời gian năm trước,Tăng / giảm,Thay đổi (%)");
            foreach (var item in model.SoSanhCungKy)
                sb.AppendLine($"{Esc(item.Label)},{Esc(item.Unit)},{Num(item.Current)},{Num(item.Previous)},{Num(item.Difference)},{(item.Percent.HasValue ? Num(item.Percent.Value) : "Không tính được vì năm trước bằng 0")}");
        }
        var safePeriod = model.NhanKy.Replace('/', '-').Replace(' ', '_');
        return ($"BaoCao_{model.Tab}_{safePeriod}.csv", Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static void AppendBienDong(StringBuilder sb, BienDongNhanSuReport report)
    {
        sb.AppendLine("BIẾN ĐỘNG NHÂN SỰ");
        sb.AppendLine("Chỉ tiêu,Giá trị");
        sb.AppendLine($"Nhân sự đầu kỳ,{report.DauKy}");
        sb.AppendLine($"Tuyển mới trong kỳ,{report.TuyenMoi}");
        sb.AppendLine($"Thôi việc trong kỳ,{report.NghiViec}");
        sb.AppendLine($"Nhân sự cuối kỳ,{report.CuoiKy}");
        sb.AppendLine($"Biến động ròng,{report.BienDongRong}");
        sb.AppendLine($"Tỷ lệ thôi việc (%),{Num(report.TyLeThoiViec)}");
        sb.AppendLine();

        sb.AppendLine("DIỄN BIẾN THEO THÁNG");
        sb.AppendLine("Tháng,Tuyển mới,Thôi việc,Quân số cuối tháng");
        foreach (var item in report.Series)
        {
            sb.AppendLine($"{item.Nhan},{item.TuyenMoi},{item.NghiViec},{item.CuoiKy}");
        }

        sb.AppendLine();
        sb.AppendLine("THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Quân số,Tuyển mới,Thôi việc");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.PhongBan)},{item.QuanSo},{item.TuyenMoi},{item.NghiViec}");
        }
    }

    private static void AppendCoCau(StringBuilder sb, BaoCaoViewModel model)
    {
        var report = model.CoCau;
        sb.AppendLine("CƠ CẤU NHÂN LỰC");
        sb.AppendLine($"Nhân sự cuối kỳ,{model.BienDong.CuoiKy}");
        sb.AppendLine($"Nghỉ việc trong kỳ,{model.BienDong.NghiViec}");
        sb.AppendLine($"Hồ sơ thôi việc thiếu ngày,{model.BienDong.SoHoSoThieuNgayThoiViec}");
        sb.AppendLine("Trạng thái và phòng ban trong quá khứ có thể không còn đầy đủ.");
        sb.AppendLine($"Tổng nhân sự,{report.TongNhanSu}");
        sb.AppendLine($"Đang làm việc,{report.DangLam}");
        sb.AppendLine($"Đang nghỉ phép,{report.NghiPhep}");
        sb.AppendLine($"Có nghỉ phép trong kỳ (đơn đã duyệt, nhân viên duy nhất),{report.CoNghiPhepTrongKy}");
        sb.AppendLine("Lưu ý: người nghỉ phép có thể đồng thời thuộc nhóm trạng thái khác.");
        sb.AppendLine($"Đã thôi việc,{report.DaThoiViec}");
        sb.AppendLine();
        AppendPhanBo(sb, "THEO PHÒNG BAN", report.TheoPhongBan);
        AppendPhanBo(sb, "THEO TRẠNG THÁI", report.TheoTrangThai);
        AppendPhanBo(sb, "THEO GIỚI TÍNH", report.TheoGioiTinh);
        AppendPhanBo(sb, "THEO CHỨC VỤ", report.TheoChucVu);
    }

    private static void AppendHocVan(StringBuilder sb, HocVanThamNienReport report)
    {
        sb.AppendLine("HỌC VẤN, THÂM NIÊN & MỨC LƯƠNG");
        sb.AppendLine($"Số nhân sự từ đại học trở lên,{report.SoDaiHocTroLen}");
        sb.AppendLine($"Tỷ lệ từ đại học trở lên (%),{Num(report.TyLeDaiHocTroLen)}");
        sb.AppendLine($"Thâm niên trung bình (năm),{Num(report.ThamNienTrungBinh)}");
        if (report.CanViewSalaryInfo)
        {
            sb.AppendLine($"Số nhân sự có lương cơ bản,{report.SoNhanSuCoLuong}");
            sb.AppendLine($"Số nhân sự chưa có lương cơ bản,{report.SoNhanSuThieuLuong}");
            sb.AppendLine($"Mức lương cơ bản trung bình (đ),{(report.SoNhanSuCoLuong > 0 ? Num(report.LuongCoBanTrungBinh) : "Chưa có dữ liệu")}");
            sb.AppendLine($"Mức lương cơ bản cao nhất (đ),{(report.SoNhanSuCoLuong > 0 ? Num(report.LuongCoBanCaoNhat) : "Chưa có dữ liệu")}");
            sb.AppendLine($"Mức lương cơ bản thấp nhất (đ),{(report.SoNhanSuCoLuong > 0 ? Num(report.LuongCoBanThapNhat) : "Chưa có dữ liệu")}");
        }
        sb.AppendLine();
        AppendPhanBo(sb, "THEO TRÌNH ĐỘ", report.TheoTrinhDo);
        AppendPhanBo(sb, "THEO THÂM NIÊN", report.TheoThamNien);
        if (report.CanViewSalaryInfo && report.TheoMucLuong.Count > 0)
        {
            AppendPhanBo(sb, "THEO DẢI MỨC LƯƠNG CƠ BẢN", report.TheoMucLuong);
        }
    }

    private static void AppendQuyLuong(StringBuilder sb, QuyLuongNganSachReport report)
    {
        sb.AppendLine("ĐỐI SOÁT QUỸ LƯƠNG VS ĐỊNH MỨC THAM CHIẾU");
        sb.AppendLine($"Lương thực nhận đã tính,{Num(report.ThucChi)}");
        sb.AppendLine($"Tổng lương đã tính (Gross),{Num(report.ThucChiGross)}");
        sb.AppendLine($"{Esc(report.NhanDinhMucThamChieu)},{Num(report.NganSachThamChieu)}");
        sb.AppendLine($"Chênh lệch,{Num(report.ChenhLech)}");
        sb.AppendLine($"Tỷ lệ so với định mức (%),{Num(report.PhanTramGiaiNgan)}");
        sb.AppendLine($"Vượt định mức,{(report.VuotNguong ? "Có" : "Không")}");
        sb.AppendLine($"Ngân sách được duyệt,{(report.NganSachDuocDuyet.HasValue ? Num(report.NganSachDuocDuyet.Value) : "Chưa đủ dữ liệu")}");
        sb.AppendLine($"Trạng thái ngân sách,{Esc(report.TrangThaiNganSachDuyet)}");
        sb.AppendLine($"Ghi chú,{Esc(report.GhiChuPhuongPhap)}");
        sb.AppendLine();

        sb.AppendLine("DIỄN BIẾN THEO THÁNG");
        sb.AppendLine("Tháng,Định mức tham chiếu,Thực chi,Chênh lệch");
        foreach (var item in report.Series)
        {
            sb.AppendLine($"{item.Nhan},{Num(item.NganSach)},{Num(item.ThucChi)},{Num(item.ThucChi - item.NganSach)}");
        }

        sb.AppendLine();
        sb.AppendLine("THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Định mức tham chiếu,Thực chi,Chênh lệch");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.PhongBan)},{Num(item.NganSach)},{Num(item.ThucChi)},{Num(item.ChenhLech)}");
        }
    }

    private static void AppendChuyenCan(StringBuilder sb, ChuyenCanViPhamReport report)
    {
        sb.AppendLine("CHUYÊN CẦN & VI PHẠM");
        sb.AppendLine($"Tổng lượt chấm công,{report.TongBanGhi}");
        sb.AppendLine($"Đúng giờ,{report.DungGio}");
        sb.AppendLine($"Đi trễ,{report.DiTre}");
        sb.AppendLine($"Về sớm,{report.VeSom}");
        sb.AppendLine($"Trễ và sớm,{report.TreVaSom}");
        sb.AppendLine($"Vắng mặt,{report.Vang}");
        sb.AppendLine($"Tỷ lệ chuyên cần (%),{Num(report.TyLeChuyenCan)}");
        sb.AppendLine($"Tổng phút đi trễ,{report.TongPhutTre}");
        sb.AppendLine($"Tổng phút về sớm,{report.TongPhutVeSom}");
        sb.AppendLine($"Tổng giờ tăng ca OT,{Num(report.TongGioOt)}");
        sb.AppendLine();

        sb.AppendLine("XẾP HẠNG VI PHẠM");
        sb.AppendLine("Nhân viên,Phòng ban,Số lần vi phạm,Tổng phút trễ,Tổng phút sớm");
        foreach (var item in report.XepHangViPham)
        {
            sb.AppendLine($"{Esc(item.HoTen)},{Esc(item.PhongBan)},{item.SoLanViPham},{item.TongPhutTre},{item.TongPhutVeSom}");
        }

        sb.AppendLine();
        sb.AppendLine("TỶ LỆ CHUYÊN CẦN THEO PHÒNG BAN");
        sb.AppendLine("Phòng ban,Số lượt,Tỷ lệ (%)");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"{Esc(item.Nhan)},{item.GiaTri},{Num(item.TyLe)}");
        }
    }

    private static void AppendSuKien(StringBuilder sb, HieuQuaSuKienReport report)
    {
        sb.AppendLine("HIỆU QUẢ NHÂN SỰ DỰ ÁN / SỰ KIỆN");
        sb.AppendLine($"Tổng số sự kiện,{report.TongSuKien}");
        sb.AppendLine($"Đang diễn ra,{report.DangDienRa}");
        sb.AppendLine($"Đã hoàn thành,{report.DaHoanThanh}");
        sb.AppendLine($"Đã hủy,{report.DaHuy}");
        sb.AppendLine($"Tổng lượt phân công,{report.TongLuotPhanCong}");
        sb.AppendLine($"Tổng ngân sách phụ cấp,{Num(report.TongNganSach)}");
        sb.AppendLine($"Tổng phụ cấp đã phân bổ,{Num(report.TongPhuCapDaPhanBo)}");
        sb.AppendLine($"Ghi chú kỳ tiền,{Esc(report.GhiChuPhamVi)}");
        sb.AppendLine($"Tỷ lệ xuất sắc (%),{Num(report.TyLeXuatSac)}");
        sb.AppendLine($"Tỷ lệ hoàn thành (%),{Num(report.TyLeHoanThanh)}");
        sb.AppendLine();

        sb.AppendLine("DANH SÁCH SỰ KIỆN");
        sb.AppendLine("Tên sự kiện,Loại,Trạng thái,Từ ngày,Đến ngày,Số nhân sự,Ngân sách,Đã phân bổ,Tỷ lệ sử dụng (%),Xuất sắc,Hoàn thành,Chưa đánh giá");
        foreach (var item in report.DanhSach)
        {
            sb.AppendLine(string.Join(',',
                Esc(item.TenDuAn), Esc(item.LoaiLabel), Esc(item.TrangThaiLabel),
                item.TuNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.DenNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                item.SoNhanSu, Num(item.NganSach), Num(item.PhuCapDaPhanBo), Num(item.TyLeSuDungNganSach),
                item.SoXuatSac, item.SoHoanThanh, item.SoChuaDanhGia));
        }

        sb.AppendLine();
        AppendPhanBo(sb, "THEO LOẠI SỰ KIỆN", report.TheoLoai);
        AppendPhanBo(sb, "THEO ĐÁNH GIÁ NHÂN SỰ", report.TheoDanhGia);
    }

    /// <summary>Ghi một khối phân bổ ba cột: nhãn, số lượng, tỷ lệ.</summary>
    private static void AppendPhanBo(StringBuilder sb, string title, IReadOnlyList<PhanBoItem> items)
    {
        sb.AppendLine(title);
        sb.AppendLine("Nhóm,Số lượng,Tỷ lệ (%)");
        foreach (var item in items)
        {
            sb.AppendLine($"{Esc(item.Nhan)},{item.GiaTri},{Num(item.TyLe)}");
        }

        sb.AppendLine();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TIỆN ÍCH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Giải quyết phạm vi báo cáo từ phiên đăng nhập. Trả phạm vi bị từ chối khi vai trò
    /// không được phép hoặc Quản lý thiếu claim phòng ban.
    /// </summary>
    private ReportPolicy.BaoCaoScope ResolveScope()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var raw = User.FindFirst("DepartmentId")?.Value;
        int? departmentId = int.TryParse(raw, out var id) && id > 0 ? id : null;

        return ReportPolicy.ResolveScope(role, departmentId);
    }

    /// <summary>Số thập phân gọn cho CSV, dùng dấu chấm để Excel không hiểu nhầm dấu phân cách.</summary>
    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Bọc trường CSV trong dấu nháy kép khi chứa dấu phẩy, nháy kép hoặc xuống dòng.</summary>
        /// <summary>Chuẩn hóa trường CSV an toàn, chống chèn công thức CSV Injection (ReportPolicy.EscapeCsv).</summary>
    private static string Esc(string? value) => ReportPolicy.EscapeCsv(value);

    /// <summary>Yêu cầu AJAX nạp vùng dữ liệu tại chỗ.</summary>
    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    // ═══════════════════════════════════════════════════════════════════════
    //  XUẤT EXCEL XML (SpreadsheetML)
    // ═══════════════════════════════════════════════════════════════════════

    private static (string FileName, byte[] Content) BuildExcelXml(BaoCaoViewModel model)
    {
        var safePeriod = model.NhanKy.Replace('/', '-').Replace(' ', '_');
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"11\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"Header\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"14\" ss:Bold=\"1\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"SubHeader\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"10\" ss:Italic=\"1\" ss:Color=\"#555555\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"ColHeader\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"11\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#1E3A8A\" ss:Pattern=\"Solid\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"BoldTitle\"><Font ss:FontName=\"Segoe UI\" ss:Size=\"11\" ss:Bold=\"1\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"Number\"><NumberFormat ss:Format=\"#,##0\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"Decimal\"><NumberFormat ss:Format=\"#,##0.0\"/></Style>");
        sb.AppendLine(" </Styles>");
        sb.AppendLine($" <Worksheet ss:Name=\"{XmlEsc(model.Tab)}\">");
        sb.AppendLine("  <Table>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">BÁO CÁO THỐNG KÊ NHÂN SỰ — " + XmlEsc(model.NhanKy) + "</Data></Cell></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"SubHeader\"><Data ss:Type=\"String\">Phạm vi: " + XmlEsc(model.PhamViLabel) + " | Người xuất: " + XmlEsc(model.RoleLabel) + " | Ngày tạo: " + model.GeneratedAt.ToOffset(VietnamOffset).ToString("dd/MM/yyyy HH:mm") + "</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");

        switch (model.Tab)
        {
            case "co-cau":
                AppendExcelCoCau(sb, model);
                break;
            case "hoc-van":
                AppendExcelHocVan(sb, model.HocVanThamNien);
                break;
            case "quy-luong":
                AppendExcelQuyLuong(sb, model.QuyLuong);
                break;
            case "chuyen-can":
                AppendExcelChuyenCan(sb, model.ChuyenCan);
                break;
            case "su-kien":
                AppendExcelSuKien(sb, model.SuKien);
                break;
            default:
                AppendExcelBienDong(sb, model.BienDong);
                break;
        }

        if (model.SoSanhCungKy.Count > 0)
        {
            sb.AppendLine("<Row><Cell><Data ss:Type=\"String\">SO SÁNH VỚI NĂM TRƯỚC " + XmlEsc(model.CungKyLabel) + "</Data></Cell></Row>");
            sb.AppendLine("<Row><Cell><Data ss:Type=\"String\">Nội dung (đơn vị)</Data></Cell><Cell><Data ss:Type=\"String\">Thời gian đã chọn</Data></Cell><Cell><Data ss:Type=\"String\">Cùng thời gian năm trước</Data></Cell><Cell><Data ss:Type=\"String\">Tăng / giảm</Data></Cell><Cell><Data ss:Type=\"String\">Thay đổi (%)</Data></Cell></Row>");
            foreach (var item in model.SoSanhCungKy)
                sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.Label)} ({XmlEsc(item.Unit)})</Data></Cell><Cell><Data ss:Type=\"Number\">{Num(item.Current)}</Data></Cell><Cell><Data ss:Type=\"Number\">{Num(item.Previous)}</Data></Cell><Cell><Data ss:Type=\"Number\">{Num(item.Difference)}</Data></Cell><Cell><Data ss:Type=\"String\">{(item.Percent.HasValue ? Num(item.Percent.Value) : "Không tính được vì năm trước bằng 0")}</Data></Cell></Row>");
        }
        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");

        return ($"BaoCao_{model.Tab}_{safePeriod}.xls", Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string XmlEsc(string? str) => System.Security.SecurityElement.Escape(str ?? string.Empty);

    // Build a small standards-based xlsx package from the existing typed report rows.
    // Strings are inline text, never formulas; numeric XML uses invariant culture.
    private static (string FileName, byte[] Content) BuildExcelFile(BaoCaoViewModel model)
    {
        var (_, xml) = BuildExcelXml(model);
        XNamespace legacy = "urn:schemas-microsoft-com:office:spreadsheet";
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = XDocument.Parse(Encoding.UTF8.GetString(xml)).Descendants(legacy + "Row").ToArray();
        var sheet = new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows.Select((row, index) =>
            new XElement(ns + "row", new XAttribute("r", index + 1), row.Elements(legacy + "Cell").Select((cell, col) =>
            {
                var data = cell.Element(legacy + "Data");
                var numeric = (string?)data?.Attribute(legacy + "Type") == "Number";
                var style = (string?)cell.Attribute(legacy + "StyleID") switch
                { "Number" => 1, "Decimal" => 2, "Header" or "BoldTitle" => 3, "ColHeader" => 4, _ => 0 };
                var column = ""; var n = col + 1;
                while (n > 0) { n--; column = (char)('A' + n % 26) + column; n /= 26; }
                return new XElement(ns + "c", new XAttribute("r", column + (index + 1)), new XAttribute("s", style),
                    numeric ? new object[] { new XAttribute("t", "n"), new XElement(ns + "v", data?.Value ?? "0") }
                        : new object[] { new XAttribute("t", "inlineStr"), new XElement(ns + "is",
                            new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), data?.Value ?? "")) });
            })))));
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Add("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Add("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Bao cao" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            Add("xl/styles.xml", """
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="14"/></font><font><b/><color rgb="FFFFFFFF"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1E3A8A"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
                """);
            Add("xl/worksheets/sheet1.xml", sheet.ToString(SaveOptions.DisableFormatting));
        }
        return ($"BaoCao_{model.Tab}_{model.NhanKy.Replace('/', '-').Replace(' ', '_')}.xlsx", buffer.ToArray());
    }

    private static void AppendExcelBienDong(StringBuilder sb, BienDongNhanSuReport report)
    {
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">BIẾN ĐỘNG NHÂN SỰ</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Nhân sự đầu kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.DauKy}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tuyển mới trong kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.TuyenMoi}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Thôi việc trong kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.NghiViec}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Nhân sự cuối kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.CuoiKy}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tỷ lệ thôi việc (%)</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{report.TyLeThoiViec.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Phòng ban</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Quân số</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Tuyển mới</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Thôi việc</Data></Cell></Row>");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.PhongBan)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.QuanSo}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.TuyenMoi}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.NghiViec}</Data></Cell></Row>");
        }
    }

    private static void AppendExcelCoCau(StringBuilder sb, BaoCaoViewModel model)
    {
        var report = model.CoCau;
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Nhân sự cuối kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{model.BienDong.CuoiKy}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Nghỉ việc trong kỳ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{model.BienDong.NghiViec}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Hồ sơ thôi việc thiếu ngày</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{model.BienDong.SoHoSoThieuNgayThoiViec}</Data></Cell></Row>");
        sb.AppendLine("   <Row><Cell><Data ss:Type=\"String\">Người nghỉ phép có thể thuộc nhóm trạng thái khác; thông tin lịch sử có thể không đầy đủ.</Data></Cell></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">CƠ CẤU NHÂN LỰC</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng nhân sự</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.TongNhanSu}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Đang làm việc</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.DangLam}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Đang nghỉ phép</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.NghiPhep}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Có nghỉ phép trong kỳ (đơn đã duyệt)</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.CoNghiPhepTrongKy}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Đã thôi việc</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.DaThoiViec}</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");
        AppendExcelPhanBo(sb, "THEO PHÒNG BAN", report.TheoPhongBan);
        AppendExcelPhanBo(sb, "THEO CHỨC VỤ", report.TheoChucVu);
    }

    private static void AppendExcelHocVan(StringBuilder sb, HocVanThamNienReport report)
    {
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">HỌC VẤN, THÂM NIÊN & MỨC LƯƠNG</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Số nhân sự từ đại học trở lên</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.SoDaiHocTroLen}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tỷ lệ từ đại học trở lên (%)</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{report.TyLeDaiHocTroLen.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Thâm niên trung bình (năm)</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{report.ThamNienTrungBinh.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        if (report.CanViewSalaryInfo)
        {
            sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">Nhân sự có lương cơ bản</Data></Cell><Cell><Data ss:Type=\"Number\">{report.SoNhanSuCoLuong}</Data></Cell></Row>");
            sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">Nhân sự chưa có lương cơ bản</Data></Cell><Cell><Data ss:Type=\"Number\">{report.SoNhanSuThieuLuong}</Data></Cell></Row>");
            if (report.SoNhanSuCoLuong > 0)
            {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Mức lương cơ bản trung bình (đ)</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.LuongCoBanTrungBinh)}</Data></Cell></Row>");
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Mức lương cơ bản cao nhất (đ)</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.LuongCoBanCaoNhat)}</Data></Cell></Row>");
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Mức lương cơ bản thấp nhất (đ)</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.LuongCoBanThapNhat)}</Data></Cell></Row>");
            }
            else sb.AppendLine("<Row><Cell><Data ss:Type=\"String\">Chưa có dữ liệu lương cơ bản</Data></Cell></Row>");
        }
        sb.AppendLine("   <Row></Row>");
        AppendExcelPhanBo(sb, "THEO TRÌNH ĐỘ", report.TheoTrinhDo);
        AppendExcelPhanBo(sb, "THEO THÂM NIÊN", report.TheoThamNien);
        if (report.CanViewSalaryInfo && report.TheoMucLuong.Count > 0)
        {
            AppendExcelPhanBo(sb, "THEO DẢI MỨC LƯƠNG CƠ BẢN", report.TheoMucLuong);
        }
    }

    private static void AppendExcelQuyLuong(StringBuilder sb, QuyLuongNganSachReport report)
    {
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">ĐỐI SOÁT QUỸ LƯƠNG VS ĐỊNH MỨC THAM CHIẾU</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Lương thực nhận đã tính</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.ThucChi)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng lương đã tính (Gross)</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.ThucChiGross)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(report.NhanDinhMucThamChieu)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.NganSachThamChieu)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Chênh lệch</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.ChenhLech)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tỷ lệ so với định mức (%)</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{report.PhanTramGiaiNgan.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");
        sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">Ngân sách được duyệt</Data></Cell><Cell><Data ss:Type=\"String\">{(report.NganSachDuocDuyet.HasValue ? Num(report.NganSachDuocDuyet.Value) : "Chưa đủ dữ liệu")}</Data></Cell></Row>");
        sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">{XmlEsc(report.TrangThaiNganSachDuyet)}</Data></Cell></Row>");
        sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">{XmlEsc(report.GhiChuPhuongPhap)}</Data></Cell></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Phòng ban</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Định mức</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Thực chi</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Chênh lệch</Data></Cell></Row>");
        foreach (var item in report.TheoPhongBan)
        {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.PhongBan)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(item.NganSach)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(item.ThucChi)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(item.ChenhLech)}</Data></Cell></Row>");
        }
    }

    private static void AppendExcelChuyenCan(StringBuilder sb, ChuyenCanViPhamReport report)
    {
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">CHUYÊN CẦN & VI PHẠM</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng lượt chấm công</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.TongBanGhi}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Đúng giờ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.DungGio}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tỷ lệ chuyên cần (%)</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{report.TyLeChuyenCan.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Nhân viên</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Phòng ban</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Số lần vi phạm</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Tổng phút trễ</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Tổng phút sớm</Data></Cell></Row>");
        foreach (var item in report.XepHangViPham)
        {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.HoTen)}</Data></Cell><Cell><Data ss:Type=\"String\">{XmlEsc(item.PhongBan)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.SoLanViPham}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.TongPhutTre}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.TongPhutVeSom}</Data></Cell></Row>");
        }
    }

    private static void AppendExcelSuKien(StringBuilder sb, HieuQuaSuKienReport report)
    {
        sb.AppendLine("   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">HIỆU QUẢ NHÂN SỰ DỰ ÁN / SỰ KIỆN</Data></Cell></Row>");
        sb.AppendLine($"<Row><Cell><Data ss:Type=\"String\">{XmlEsc(report.GhiChuPhamVi)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng số sự kiện</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.TongSuKien}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng lượt phân công</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{report.TongLuotPhanCong}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng ngân sách phụ cấp</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.TongNganSach)}</Data></Cell></Row>");
        sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">Tổng phụ cấp đã phân bổ</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(report.TongPhuCapDaPhanBo)}</Data></Cell></Row>");
        sb.AppendLine("   <Row></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Tên sự kiện</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Loại</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Trạng thái</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Nhân sự</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Ngân sách</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Đã phân bổ</Data></Cell></Row>");
        foreach (var item in report.DanhSach)
        {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.TenDuAn)}</Data></Cell><Cell><Data ss:Type=\"String\">{XmlEsc(item.LoaiLabel)}</Data></Cell><Cell><Data ss:Type=\"String\">{XmlEsc(item.TrangThaiLabel)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.SoNhanSu}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(item.NganSach)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{Num(item.PhuCapDaPhanBo)}</Data></Cell></Row>");
        }
    }

    private static void AppendExcelPhanBo(StringBuilder sb, string title, IReadOnlyList<PhanBoItem> items)
    {
        sb.AppendLine($"   <Row><Cell ss:StyleID=\"BoldTitle\"><Data ss:Type=\"String\">{XmlEsc(title)}</Data></Cell></Row>");
        sb.AppendLine("   <Row><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Nhóm</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Số lượng</Data></Cell><Cell ss:StyleID=\"ColHeader\"><Data ss:Type=\"String\">Tỷ lệ (%)</Data></Cell></Row>");
        foreach (var item in items)
        {
            sb.AppendLine($"   <Row><Cell><Data ss:Type=\"String\">{XmlEsc(item.Nhan)}</Data></Cell><Cell ss:StyleID=\"Number\"><Data ss:Type=\"Number\">{item.GiaTri}</Data></Cell><Cell ss:StyleID=\"Decimal\"><Data ss:Type=\"Number\">{item.TyLe.ToString("0.0", CultureInfo.InvariantCulture)}</Data></Cell></Row>");
        }
        sb.AppendLine("   <Row></Row>");
    }

}
