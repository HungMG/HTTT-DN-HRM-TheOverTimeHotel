using System.Globalization;
using System.Security.Claims;
using System.Text;
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

        try
        {
            var model = await BuildAsync(scope, ky, nam, thang, quy, phongBan, tab);
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
                AppendCoCau(sb, model.CoCau);
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

        var safePeriod = model.NhanKy.Replace('/', '-').Replace(' ', '_');
        return ($"BaoCao_{model.Tab}_{safePeriod}.csv", Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static void AppendBienDong(StringBuilder sb, BienDongNhanSuReport report)
    {
        sb.AppendLine("BIẾN ĐỘNG NHÂN SỰ");
        sb.AppendLine("Chỉ tiêu,Giá trị");
        sb.AppendLine($"Quân số đầu kỳ,{report.DauKy}");
        sb.AppendLine($"Tuyển mới trong kỳ,{report.TuyenMoi}");
        sb.AppendLine($"Thôi việc trong kỳ,{report.NghiViec}");
        sb.AppendLine($"Quân số cuối kỳ,{report.CuoiKy}");
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

    private static void AppendCoCau(StringBuilder sb, CoCauNhanLucReport report)
    {
        sb.AppendLine("CƠ CẤU NHÂN LỰC");
        sb.AppendLine($"Tổng nhân sự,{report.TongNhanSu}");
        sb.AppendLine($"Đang làm việc,{report.DangLam}");
        sb.AppendLine($"Đang nghỉ phép,{report.NghiPhep}");
        sb.AppendLine($"Đã thôi việc,{report.DaThoiViec}");
        sb.AppendLine();
        AppendPhanBo(sb, "THEO PHÒNG BAN", report.TheoPhongBan);
        AppendPhanBo(sb, "THEO TRẠNG THÁI", report.TheoTrangThai);
        AppendPhanBo(sb, "THEO GIỚI TÍNH", report.TheoGioiTinh);
        AppendPhanBo(sb, "THEO CHỨC VỤ", report.TheoChucVu);
    }

    private static void AppendHocVan(StringBuilder sb, HocVanThamNienReport report)
    {
        sb.AppendLine("HỌC VẤN & THÂM NIÊN");
        sb.AppendLine($"Số nhân sự từ đại học trở lên,{report.SoDaiHocTroLen}");
        sb.AppendLine($"Tỷ lệ từ đại học trở lên (%),{Num(report.TyLeDaiHocTroLen)}");
        sb.AppendLine($"Thâm niên trung bình (năm),{Num(report.ThamNienTrungBinh)}");
        sb.AppendLine();
        AppendPhanBo(sb, "THEO TRÌNH ĐỘ", report.TheoTrinhDo);
        AppendPhanBo(sb, "THEO THÂM NIÊN", report.TheoThamNien);
    }

    private static void AppendQuyLuong(StringBuilder sb, QuyLuongNganSachReport report)
    {
        sb.AppendLine("ĐỐI SOÁT QUỸ LƯƠNG VS ĐỊNH MỨC THAM CHIẾU");
        sb.AppendLine($"Thực chi (thực nhận),{Num(report.ThucChi)}");
        sb.AppendLine($"Thực chi (gộp),{Num(report.ThucChiGross)}");
        sb.AppendLine($"Định mức tham chiếu,{Num(report.NganSachThamChieu)}");
        sb.AppendLine($"Chênh lệch,{Num(report.ChenhLech)}");
        sb.AppendLine($"Tỷ lệ giải ngân (%),{Num(report.PhanTramGiaiNgan)}");
        sb.AppendLine($"Vượt định mức,{(report.VuotNguong ? "Có" : "Không")}");
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
    private static string Esc(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Yêu cầu AJAX nạp vùng dữ liệu tại chỗ.</summary>
    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
