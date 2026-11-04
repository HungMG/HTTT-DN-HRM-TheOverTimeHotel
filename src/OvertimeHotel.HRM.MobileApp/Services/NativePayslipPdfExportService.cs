using System.Globalization;
using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Utilities;

#if ANDROID
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidCanvas = Android.Graphics.Canvas;
using AndroidColor = Android.Graphics.Color;
using AndroidPaint = Android.Graphics.Paint;
using AndroidPdfDocument = Android.Graphics.Pdf.PdfDocument;
using AndroidRectF = Android.Graphics.RectF;
using AndroidTypeface = Android.Graphics.Typeface;
#endif

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Xuất phiếu lương PDF chuẩn A4 bằng Android Native Graphics API (Android.Graphics.Pdf).
/// Không phụ thuộc thư viện native C++ bên ngoài, hoạt động mượt mà 100% trên mọi kiến trúc Android.
/// </summary>
public sealed class NativePayslipPdfExportService : IPayslipPdfExportService
{
    public Task<string> ExportAsync(
        PayslipSummaryDto summary,
        IReadOnlyList<PayslipItemDto> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(items);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            var fileName = $"Phieu_Luong_{summary.Nam:0000}_{summary.Thang:00}_{summary.MaPhieuLuong}.pdf";
            var cacheFolder = FileSystem.CacheDirectory;
            Directory.CreateDirectory(cacheFolder);
            var filePath = Path.Combine(cacheFolder, fileName);

#if ANDROID
            GenerateAndroidPdf(summary, items, filePath);
            SaveToPublicDownloads(filePath, fileName);
#else
            File.WriteAllText(filePath, "PDF Export is natively supported on Android.");
#endif

            return filePath;
        }, cancellationToken);
    }

    public Task<string> ExportYearlyAsync(
        int year,
        string employeeName,
        string departmentRole,
        IReadOnlyList<PayslipSummaryDto> yearlySlips,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(yearlySlips);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            var fileName = $"Bang_Thu_Nhap_Nam_{year:0000}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var cacheFolder = FileSystem.CacheDirectory;
            Directory.CreateDirectory(cacheFolder);
            var filePath = Path.Combine(cacheFolder, fileName);

#if ANDROID
            GenerateAndroidYearlyPdf(year, employeeName, departmentRole, yearlySlips, filePath);
            SaveToPublicDownloads(filePath, fileName);
#else
            File.WriteAllText(filePath, "PDF Export is natively supported on Android.");
#endif

            return filePath;
        }, cancellationToken);
    }

#if ANDROID
    private static void GenerateAndroidPdf(
        PayslipSummaryDto summary,
        IReadOnlyList<PayslipItemDto> items,
        string outputPath)
    {
        using var pdfDoc = new AndroidPdfDocument();
        // Chuẩn trang A4: 595 x 842 points (72 DPI)
        var pageInfo = new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create();
        if (pageInfo == null) throw new InvalidOperationException("Không thể khởi tạo thông tin trang PDF.");

        var page = pdfDoc.StartPage(pageInfo);
        if (page == null) throw new InvalidOperationException("Không thể bắt đầu trang PDF.");
        var canvas = page.Canvas;
        if (canvas == null) throw new InvalidOperationException("Không thể khởi tạo bề mặt vẽ PDF.");

        canvas.DrawColor(AndroidColor.White);

        using var paint = new AndroidPaint { AntiAlias = true };

        var gross = summary.TienLuongCong + summary.TienTangCa + summary.TongPhuCap + summary.TongThuong;
        var period = summary.Thang > 0 && summary.Nam > 0
            ? $"Tháng {summary.Thang:00} / {summary.Nam:0000}"
            : $"Kỳ lương #{summary.MaPhieuLuong}";

        var incomeItems = items
            .Where(x => !string.Equals(x.LoaiKhoan, "KHAU_TRU", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var deductionItems = items
            .Where(x => string.Equals(x.LoaiKhoan, "KHAU_TRU", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        const float left = 36f;
        const float right = 559f;
        const float width = right - left; // 523f

        // --- 1. TIÊU ĐỀ KHÁCH SẠN 5 SAO & KỲ LƯƠNG ---
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 14f;
        paint.Color = AndroidColor.ParseColor("#1E3A8A"); // Xanh Navy Hoàng Gia
        canvas.DrawText("THE OVERTIME HOTEL", left, 48f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 8f;
        paint.Color = AndroidColor.ParseColor("#B45309"); // Vàng kim
        canvas.DrawText("★ ★ ★ ★ ★  LUXURY HOTEL & SUITES", left, 59f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 16f;
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("PHIẾU LƯƠNG ĐIỆN TỬ", right, 48f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 10f;
        paint.Color = AndroidColor.ParseColor("#2563EB");
        canvas.DrawText(period, right, 62f, paint);

        paint.TextAlign = AndroidPaint.Align.Left;

        // Dải viền vàng kim phân cách
        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 2f;
        paint.Color = AndroidColor.ParseColor("#D4AF37");
        canvas.DrawLine(left, 70f, right, 70f, paint);

        var currentY = 82f;

        // --- 2. KHUNG THÔNG TIN NHÂN SỰ ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#F8FAFC");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 50f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 1f;
        paint.Color = AndroidColor.ParseColor("#E2E8F0");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 50f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.TextSize = 9f;

        // Cột trái: Mã phiếu & Họ tên
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Mã phiếu:", left + 12f, currentY + 20f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        canvas.DrawText($"#{summary.MaPhieuLuong:D4}", left + 65f, currentY + 20f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Họ và tên:", left + 12f, currentY + 38f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        canvas.DrawText(summary.HoTen ?? "Nhân viên", left + 65f, currentY + 38f, paint);

        // Cột phải: Phòng ban & Chức vụ
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Phòng ban:", left + 260f, currentY + 20f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        canvas.DrawText(summary.TenPhongBan ?? "Khách Sạn", left + 325f, currentY + 20f, paint);

        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Chức vụ:", left + 260f, currentY + 38f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        canvas.DrawText(summary.TenChucVu ?? "Nhân viên", left + 325f, currentY + 38f, paint);

        currentY += 58f;

        // --- 3. BĂNG LƯƠNG THỰC NHẬN (ROYAL NAVY BANNER) ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#1E3A8A");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 62f), 8f, 8f, paint);

        // Nhãn NET
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.Color = AndroidColor.White;
        paint.TextSize = 10f;
        canvas.DrawText("LƯƠNG THỰC NHẬN (NET):", left + 14f, currentY + 20f, paint);

        // Số tiền NET vàng kim nổi bật
        paint.Color = AndroidColor.ParseColor("#FDE047");
        paint.TextSize = 18f;
        canvas.DrawText($"{Format(summary.ThucNhan)} ₫", left + 14f, currentY + 42f, paint);

        // Đọc số tiền bằng chữ tiếng Việt
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Italic));
        paint.Color = AndroidColor.ParseColor("#E0E7FF");
        paint.TextSize = 8.5f;
        var inWords = VietnameseNumberToWords.ConvertToWords(summary.ThucNhan);
        canvas.DrawText($"Bằng chữ: {inWords}", left + 14f, currentY + 55f, paint);

        currentY += 70f;

        // --- 4. BA Ô CHỈ SỐ TỔNG HỢP (GROSS / KHẤU TRỪ / CÔNG) ---
        const float gap = 8f;
        var boxWidth = (width - gap * 2f) / 3f;

        // Thẻ 1: Tổng Gross
        DrawKpiBox(canvas, paint, left, currentY, boxWidth, 40f,
            "#F0FDF4", "#BBF7D0", "TỔNG GROSS", "#065F46",
            $"+{Format(gross)} ₫", "#047857");

        // Thẻ 2: Tổng Khấu trừ
        DrawKpiBox(canvas, paint, left + boxWidth + gap, currentY, boxWidth, 40f,
            "#FEF2F2", "#FECACA", "TỔNG KHẤU TRỪ", "#991B1B",
            $"-{Format(summary.TongKhauTru)} ₫", "#B91C1C");

        // Thẻ 3: Ngày công
        DrawKpiBox(canvas, paint, left + (boxWidth + gap) * 2f, currentY, boxWidth, 40f,
            "#EFF6FF", "#BFDBFE", "NGÀY CÔNG THỰC TẾ", "#1E40AF",
            $"{summary.NgayCongThucTe:0.##} / 26 công", "#1E3A8A");

        currentY += 48f;

        // --- 5. BẢNG TẦNG 1: THU NHẬP (GROSS) ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 11f;
        paint.Color = AndroidColor.ParseColor("#047857");
        canvas.DrawText("TẦNG 1 — THU NHẬP (GROSS)", left, currentY + 11f, paint);

        currentY += 16f;

        // Table Header
        DrawTableHeader(canvas, paint, left, right, currentY);
        currentY += 18f;

        // Dòng Lương ngày công
        DrawTableRow(canvas, paint, left, right, currentY,
            "Tiền lương theo ngày công",
            $"({Format(summary.LuongCoBan)} / 26) × {summary.NgayCongThucTe:0.##} ngày",
            $"+{Format(summary.TienLuongCong)} ₫",
            false);
        currentY += 18f;

        // Dòng Làm thêm giờ (OT) nếu có
        if (summary.TienTangCa > 0)
        {
            DrawTableRow(canvas, paint, left, right, currentY,
                "Tiền làm thêm giờ (OT)",
                "Theo hệ số làm thêm ca đêm / ngày nghỉ",
                $"+{Format(summary.TienTangCa)} ₫",
                false);
            currentY += 18f;
        }

        // Các dòng phụ cấp / thưởng
        foreach (var item in incomeItems)
        {
            DrawTableRow(canvas, paint, left, right, currentY,
                item.TenKhoan,
                item.GhiChu ?? string.Empty,
                $"+{Format(item.SoTien)} ₫",
                false);
            currentY += 18f;
        }

        // Dòng Tổng Gross
        DrawTableRow(canvas, paint, left, right, currentY,
            "Tổng thu nhập Gross (Tầng 1)",
            string.Empty,
            $"+{Format(gross)} ₫",
            true);
        currentY += 24f;

        // --- 6. BẢNG TẦNG 2: CÁC KHOẢN KHẤU TRỪ ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 11f;
        paint.Color = AndroidColor.ParseColor("#B91C1C");
        canvas.DrawText("TẦNG 2 — CÁC KHOẢN KHẤU TRỪ", left, currentY + 11f, paint);

        currentY += 16f;

        // Table Header
        DrawTableHeader(canvas, paint, left, right, currentY);
        currentY += 18f;

        if (deductionItems.Length == 0)
        {
            DrawTableRow(canvas, paint, left, right, currentY,
                "Kỳ này không có khoản khấu trừ",
                "Đạt 100% tuân thủ & quy chế",
                "0 ₫",
                false);
            currentY += 18f;
        }
        else
        {
            foreach (var item in deductionItems)
            {
                DrawTableRow(canvas, paint, left, right, currentY,
                    item.TenKhoan,
                    item.GhiChu ?? string.Empty,
                    $"-{Format(item.SoTien)} ₫",
                    false);
                currentY += 18f;
            }
        }

        // Dòng Tổng Khấu trừ
        DrawTableRow(canvas, paint, left, right, currentY,
            "Tổng các khoản khấu trừ (Tầng 2)",
            string.Empty,
            $"-{Format(summary.TongKhauTru)} ₫",
            true);
        currentY += 24f;

        // --- 7. BĂNG CÔNG THỨC QUYẾT TOÁN 2 TẦNG ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#EFF6FF");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 34f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 1f;
        paint.Color = AndroidColor.ParseColor("#BFDBFE");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 34f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 9f;
        paint.Color = AndroidColor.ParseColor("#1E3A8A");
        canvas.DrawText("Công thức quyết toán: Thực Nhận (Net) = Tổng Gross − Tổng Khấu Trừ", left + 12f, currentY + 14f, paint);

        paint.Color = AndroidColor.ParseColor("#0F172A");
        canvas.DrawText($"{Format(summary.ThucNhan)} ₫ = {Format(gross)} ₫ − {Format(summary.TongKhauTru)} ₫", left + 12f, currentY + 27f, paint);

        currentY += 46f;

        // --- 8. PHẦN CHÂN TRANG (FOOTER) ---
        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 0.8f;
        paint.Color = AndroidColor.ParseColor("#E2E8F0");
        canvas.DrawLine(left, 780f, right, 780f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 7.5f;
        paint.Color = AndroidColor.ParseColor("#94A3B8");
        paint.TextAlign = AndroidPaint.Align.Center;
        canvas.DrawText("Phiếu lương điện tử được bảo mật và xác thực bởi Ban Giám Đốc và Bộ phận Kế toán The OverTime Hotel.", (left + right) / 2f, 794f, paint);
        canvas.DrawText($"Trạng thái: Đã chốt & ban hành chính thức • Ngày tạo: {DateTime.Now:dd/MM/yyyy HH:mm}", (left + right) / 2f, 806f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;

        pdfDoc.FinishPage(page);

        using var fileStream = File.Create(outputPath);
        pdfDoc.WriteTo(fileStream);
    }

    private static void GenerateAndroidYearlyPdf(
        int year,
        string employeeName,
        string departmentRole,
        IReadOnlyList<PayslipSummaryDto> yearlySlips,
        string outputPath)
    {
        using var pdfDoc = new AndroidPdfDocument();
        var pageInfo = new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create();
        if (pageInfo == null) throw new InvalidOperationException("Không thể khởi tạo thông tin trang PDF.");

        var page = pdfDoc.StartPage(pageInfo);
        if (page == null) throw new InvalidOperationException("Không thể bắt đầu trang PDF.");
        var canvas = page.Canvas;
        if (canvas == null) throw new InvalidOperationException("Không thể khởi tạo bề mặt vẽ PDF.");

        canvas.DrawColor(AndroidColor.White);

        using var paint = new AndroidPaint { AntiAlias = true };

        const float left = 36f;
        const float right = 559f;
        const float width = right - left; // 523f

        var orderedSlips = yearlySlips.OrderBy(s => s.Thang).ToArray();
        var totalGross = orderedSlips.Sum(s => s.TienLuongCong + s.TienTangCa + s.TongPhuCap + s.TongThuong);
        var totalDeductions = orderedSlips.Sum(s => s.TongKhauTru);
        var totalNet = orderedSlips.Sum(s => s.ThucNhan);
        var totalWorkDays = orderedSlips.Sum(s => s.NgayCongThucTe);

        // --- 1. TIÊU ĐỀ KHÁCH SẠN 5 SAO & BẢNG THU NHẬP NĂM ---
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 14f;
        paint.Color = AndroidColor.ParseColor("#1E3A8A"); // Xanh Navy Hoàng Gia
        canvas.DrawText("THE OVERTIME HOTEL", left, 46f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 8f;
        paint.Color = AndroidColor.ParseColor("#B45309"); // Vàng kim
        canvas.DrawText("★ ★ ★ ★ ★  LUXURY HOTEL & SUITES", left, 57f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 15f;
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("BẢNG TỔNG HỢP THU NHẬP NĂM", right, 46f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 11f;
        paint.Color = AndroidColor.ParseColor("#2563EB");
        canvas.DrawText($"QUYẾT TOÁN THU NHẬP NĂM {year:0000}", right, 59f, paint);

        paint.TextAlign = AndroidPaint.Align.Left;

        // Dải viền vàng kim phân cách
        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 2f;
        paint.Color = AndroidColor.ParseColor("#D4AF37");
        canvas.DrawLine(left, 67f, right, 67f, paint);

        var currentY = 78f;

        // --- 2. KHUNG THÔNG TIN NHÂN SỰ ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#F8FAFC");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 46f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 1f;
        paint.Color = AndroidColor.ParseColor("#E2E8F0");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 46f), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.TextSize = 9f;

        // Cột trái: Họ tên & Đơn vị
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Họ và tên:", left + 12f, currentY + 18f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        canvas.DrawText(string.IsNullOrWhiteSpace(employeeName) ? "Nhân viên" : employeeName, left + 70f, currentY + 18f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Đơn vị / Vị trí:", left + 12f, currentY + 34f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        canvas.DrawText(string.IsNullOrWhiteSpace(departmentRole) ? "Khách Sạn The OverTime" : departmentRole, left + 70f, currentY + 34f, paint);

        // Cột phải: Số kỳ & Ngày lập
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Số kỳ quyết toán:", left + 300f, currentY + 18f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        canvas.DrawText($"{orderedSlips.Length} kỳ lương", left + 395f, currentY + 18f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("Ngày kết xuất:", left + 300f, currentY + 34f, paint);
        paint.Color = AndroidColor.ParseColor("#0F172A");
        canvas.DrawText(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), left + 395f, currentY + 34f, paint);

        currentY += 54f;

        // --- 3. BĂNG LƯƠNG THỰC NHẬN CẢ NĂM (ROYAL NAVY BANNER) ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#1E3A8A");
        canvas.DrawRoundRect(new AndroidRectF(left, currentY, right, currentY + 58f), 8f, 8f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.Color = AndroidColor.White;
        paint.TextSize = 10f;
        canvas.DrawText($"TỔNG LƯƠNG THỰC NHẬN NĂM {year:0000} (NET):", left + 14f, currentY + 18f, paint);

        paint.Color = AndroidColor.ParseColor("#FDE047"); // Vàng kim
        paint.TextSize = 18f;
        canvas.DrawText($"{Format(totalNet)} ₫", left + 14f, currentY + 39f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Italic));
        paint.Color = AndroidColor.ParseColor("#E0E7FF");
        paint.TextSize = 8f;
        var inWords = VietnameseNumberToWords.ConvertToWords(totalNet);
        canvas.DrawText($"Bằng chữ: {inWords}", left + 14f, currentY + 51f, paint);

        currentY += 66f;

        // --- 4. BA Ô CHỈ SỐ TỔNG HỢP CẢ NĂM (GROSS / KHẤU TRỪ / CÔNG) ---
        const float gap = 8f;
        var boxWidth = (width - gap * 2f) / 3f;

        DrawKpiBox(canvas, paint, left, currentY, boxWidth, 38f,
            "#F0FDF4", "#BBF7D0", $"TỔNG GROSS NĂM {year}", "#065F46",
            $"+{Format(totalGross)} ₫", "#047857");

        DrawKpiBox(canvas, paint, left + boxWidth + gap, currentY, boxWidth, 38f,
            "#FEF2F2", "#FECACA", $"TỔNG KHẤU TRỪ NĂM {year}", "#991B1B",
            $"-{Format(totalDeductions)} ₫", "#B91C1C");

        DrawKpiBox(canvas, paint, left + (boxWidth + gap) * 2f, currentY, boxWidth, 38f,
            "#EFF6FF", "#BFDBFE", $"TỔNG NGÀY CÔNG NĂM {year}", "#1E40AF",
            $"{totalWorkDays:0.##} ngày công", "#1E3A8A");

        currentY += 46f;

        // --- 5. BẢNG CHI TIẾT THEO TỪNG THÁNG TRONG NĂM ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 10.5f;
        paint.Color = AndroidColor.ParseColor("#1E3A8A");
        canvas.DrawText($"CHI TIẾT THU NHẬP THEO TỪNG THÁNG NĂM {year:0000}", left, currentY + 11f, paint);

        currentY += 16f;

        // Header bảng 6 cột: KỲ LƯƠNG | NGÀY CÔNG | LƯƠNG CƠ BẢN | GROSS | KHẤU TRỪ | THỰC NHẬN
        DrawYearlyTableHeader(canvas, paint, left, right, currentY);
        currentY += 18f;

        if (orderedSlips.Length == 0)
        {
            DrawYearlyTableRow(canvas, paint, left, right, currentY,
                $"Năm {year}", "0", "0 ₫", "0 ₫", "0 ₫", "0 ₫", false);
            currentY += 18f;
        }
        else
        {
            foreach (var slip in orderedSlips)
            {
                var slipGross = slip.TienLuongCong + slip.TienTangCa + slip.TongPhuCap + slip.TongThuong;
                var periodLabel = slip.Thang > 0 ? $"Tháng {slip.Thang:00}/{slip.Nam}" : $"Kỳ #{slip.MaPhieuLuong}";
                DrawYearlyTableRow(canvas, paint, left, right, currentY,
                    periodLabel,
                    $"{slip.NgayCongThucTe:0.#}",
                    $"{Format(slip.LuongCoBan)} ₫",
                    $"+{Format(slipGross)} ₫",
                    $"-{Format(slip.TongKhauTru)} ₫",
                    $"{Format(slip.ThucNhan)} ₫",
                    false);
                currentY += 18f;
            }
        }

        // Dòng Tổng Cộng Cả Năm
        DrawYearlyTableRow(canvas, paint, left, right, currentY,
            $"TỔNG CỘNG NĂM {year}",
            $"{totalWorkDays:0.#}",
            "—",
            $"+{Format(totalGross)} ₫",
            $"-{Format(totalDeductions)} ₫",
            $"{Format(totalNet)} ₫",
            true);
        currentY += 26f;

        // --- 6. PHẦN ĐỐI SOÁT & CHỮ KÝ XÁC NHẬN ---
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Italic));
        paint.TextSize = 8f;
        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText("* Số liệu thu nhập năm được tổng hợp từ các bảng lương đã được Ban Giám Đốc phê duyệt và khóa sổ chính thức.", left, currentY + 10f, paint);
        canvas.DrawText("* Bảng tổng hợp này có giá trị làm căn cứ đối soát thu nhập, hoàn thuế TNCN và các quyền lợi nhân sự theo Luật Lao Động.", left, currentY + 21f, paint);

        currentY += 36f;

        // 2 cột ký tên
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 9f;
        paint.Color = AndroidColor.ParseColor("#0F172A");

        canvas.DrawText("NGƯỜI LAO ĐỘNG", left + 40f, currentY, paint);
        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("BỘ PHẬN KẾ TOÁN & NHÂN SỰ", right - 30f, currentY, paint);
        paint.TextAlign = AndroidPaint.Align.Left;

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Italic));
        paint.TextSize = 7.5f;
        paint.Color = AndroidColor.ParseColor("#94A3B8");
        canvas.DrawText("(Ký và ghi rõ họ tên)", left + 45f, currentY + 12f, paint);
        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("(Xác thực hệ thống điện tử)", right - 40f, currentY + 12f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;

        // --- 7. FOOTER ---
        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 0.8f;
        paint.Color = AndroidColor.ParseColor("#E2E8F0");
        canvas.DrawLine(left, 785f, right, 785f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 7.5f;
        paint.Color = AndroidColor.ParseColor("#94A3B8");
        paint.TextAlign = AndroidPaint.Align.Center;
        canvas.DrawText("Bảng tổng hợp thu nhập năm được bảo mật và xác thực bởi Ban Giám Đốc và Bộ phận Kế toán The OverTime Hotel.", (left + right) / 2f, 798f, paint);
        canvas.DrawText($"Mã xác thực: {year}-HOTEL-HRM-SYS • Ngày tạo: {DateTime.Now:dd/MM/yyyy HH:mm}", (left + right) / 2f, 810f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;

        pdfDoc.FinishPage(page);

        using var fileStream = File.Create(outputPath);
        pdfDoc.WriteTo(fileStream);
    }

    private static void DrawTableHeader(AndroidCanvas canvas, AndroidPaint paint, float left, float right, float y)
    {
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#F1F5F9");
        canvas.DrawRect(left, y, right, y + 18f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 8.5f;
        paint.Color = AndroidColor.ParseColor("#475569");

        canvas.DrawText("KHOẢN MỤC", left + 6f, y + 13f, paint);
        canvas.DrawText("DIỄN GIẢI / GHI CHÚ", left + 210f, y + 13f, paint);

        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("SỐ TIỀN", right - 6f, y + 13f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;
    }

    private static void DrawTableRow(AndroidCanvas canvas, AndroidPaint paint, float left, float right, float y,
        string name, string note, string amount, bool isSubtotal)
    {
        if (isSubtotal)
        {
            paint.SetStyle(AndroidPaint.Style.Fill);
            paint.Color = AndroidColor.ParseColor("#F8FAFC");
            canvas.DrawRect(left, y, right, y + 18f, paint);

            paint.SetStyle(AndroidPaint.Style.Stroke);
            paint.StrokeWidth = 0.5f;
            paint.Color = AndroidColor.ParseColor("#E2E8F0");
            canvas.DrawLine(left, y, right, y, paint);
            canvas.DrawLine(left, y + 18f, right, y + 18f, paint);
        }

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, isSubtotal ? Android.Graphics.TypefaceStyle.Bold : Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 8.5f;
        paint.Color = isSubtotal ? AndroidColor.ParseColor("#0F172A") : AndroidColor.ParseColor("#334155");

        canvas.DrawText(name, left + 6f, y + 13f, paint);

        paint.Color = AndroidColor.ParseColor("#64748B");
        canvas.DrawText(note, left + 210f, y + 13f, paint);

        paint.Color = isSubtotal ? AndroidColor.ParseColor("#0F172A") : AndroidColor.ParseColor("#334155");
        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText(amount, right - 6f, y + 13f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;
    }

    private static void DrawYearlyTableHeader(AndroidCanvas canvas, AndroidPaint paint, float left, float right, float y)
    {
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor("#F1F5F9");
        canvas.DrawRect(left, y, right, y + 18f, paint);

        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 8f;
        paint.Color = AndroidColor.ParseColor("#475569");

        canvas.DrawText("KỲ LƯƠNG", left + 6f, y + 12f, paint);
        canvas.DrawText("CÔNG", left + 90f, y + 12f, paint);

        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText("LƯƠNG CƠ BẢN", left + 210f, y + 12f, paint);
        canvas.DrawText("GROSS", left + 320f, y + 12f, paint);
        canvas.DrawText("KHẤU TRỪ", left + 420f, y + 12f, paint);
        canvas.DrawText("THỰC NHẬN (NET)", right - 6f, y + 12f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;
    }

    private static void DrawYearlyTableRow(AndroidCanvas canvas, AndroidPaint paint, float left, float right, float y,
        string period, string workDays, string baseSalary, string gross, string deduction, string net, bool isTotal)
    {
        if (isTotal)
        {
            paint.SetStyle(AndroidPaint.Style.Fill);
            paint.Color = AndroidColor.ParseColor("#F8FAFC");
            canvas.DrawRect(left, y, right, y + 18f, paint);

            paint.SetStyle(AndroidPaint.Style.Stroke);
            paint.StrokeWidth = 0.5f;
            paint.Color = AndroidColor.ParseColor("#CBD5E1");
            canvas.DrawLine(left, y, right, y, paint);
            canvas.DrawLine(left, y + 18f, right, y + 18f, paint);
        }

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, isTotal ? Android.Graphics.TypefaceStyle.Bold : Android.Graphics.TypefaceStyle.Normal));
        paint.TextSize = 8f;
        paint.Color = isTotal ? AndroidColor.ParseColor("#0F172A") : AndroidColor.ParseColor("#334155");

        canvas.DrawText(period, left + 6f, y + 12f, paint);
        canvas.DrawText(workDays, left + 90f, y + 12f, paint);

        paint.TextAlign = AndroidPaint.Align.Right;
        canvas.DrawText(baseSalary, left + 210f, y + 12f, paint);

        paint.Color = isTotal ? AndroidColor.ParseColor("#047857") : AndroidColor.ParseColor("#059669");
        canvas.DrawText(gross, left + 320f, y + 12f, paint);

        paint.Color = isTotal ? AndroidColor.ParseColor("#B91C1C") : AndroidColor.ParseColor("#DC2626");
        canvas.DrawText(deduction, left + 420f, y + 12f, paint);

        paint.Color = isTotal ? AndroidColor.ParseColor("#1E3A8A") : AndroidColor.ParseColor("#0F172A");
        if (isTotal)
            paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        canvas.DrawText(net, right - 6f, y + 12f, paint);
        paint.TextAlign = AndroidPaint.Align.Left;
    }

    private static void DrawKpiBox(AndroidCanvas canvas, AndroidPaint paint, float x, float y, float w, float h,
        string bgHex, string borderHex, string label, string labelHex, string value, string valueHex)
    {
        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.Color = AndroidColor.ParseColor(bgHex);
        canvas.DrawRoundRect(new AndroidRectF(x, y, x + w, y + h), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Stroke);
        paint.StrokeWidth = 1f;
        paint.Color = AndroidColor.ParseColor(borderHex);
        canvas.DrawRoundRect(new AndroidRectF(x, y, x + w, y + h), 6f, 6f, paint);

        paint.SetStyle(AndroidPaint.Style.Fill);
        paint.SetTypeface(AndroidTypeface.Create(AndroidTypeface.Default, Android.Graphics.TypefaceStyle.Bold));
        paint.TextSize = 8f;
        paint.Color = AndroidColor.ParseColor(labelHex);
        canvas.DrawText(label, x + 8f, y + 14f, paint);

        paint.TextSize = 10.5f;
        paint.Color = AndroidColor.ParseColor(valueHex);
        canvas.DrawText(value, x + 8f, y + 30f, paint);
    }

    private static void SaveToPublicDownloads(string sourceFilePath, string fileName)
    {
        try
        {
            var context = Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var values = new ContentValues();
                values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
                values.Put(MediaStore.IMediaColumns.MimeType, "application/pdf");
                values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

                var uri = context.ContentResolver?.Insert(MediaStore.Downloads.ExternalContentUri, values);
                if (uri != null)
                {
                    using var inStream = File.OpenRead(sourceFilePath);
                    using var outStream = context.ContentResolver?.OpenOutputStream(uri);
                    if (outStream != null)
                    {
                        inStream.CopyTo(outStream);
                    }
                }
            }
            else
            {
                var downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
                if (downloadsDir != null)
                {
                    var destPath = Path.Combine(downloadsDir.AbsolutePath, fileName);
                    File.Copy(sourceFilePath, destPath, true);
                }
            }
        }
        catch
        {
            // Nếu lưu vào Downloads công cộng bị chặn thì file trong cache vẫn sẵn sàng cho Share API
        }
    }
#endif

    private static string Format(decimal amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
}
