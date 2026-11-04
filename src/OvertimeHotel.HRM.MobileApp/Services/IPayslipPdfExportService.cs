using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Xuất phiếu lương của chính người đang đăng nhập ra file PDF trên thiết bị.
/// </summary>
public interface IPayslipPdfExportService
{
    /// <summary>Tạo PDF từ tóm tắt và các dòng thu chi đang hiển thị của một kỳ lương.</summary>
    Task<string> ExportAsync(
        PayslipSummaryDto summary,
        IReadOnlyList<PayslipItemDto> items,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo PDF bảng tổng hợp thu nhập cả năm (Annual Statement).</summary>
    Task<string> ExportYearlyAsync(
        int year,
        string employeeName,
        string departmentRole,
        IReadOnlyList<PayslipSummaryDto> yearlySlips,
        CancellationToken cancellationToken = default);
}
