using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>
/// Đọc phiếu lương và các dòng thu chi của CHÍNH người đang đăng nhập.
/// </summary>
/// <remarks>
/// Ràng buộc bảo mật: RLS <c>doc_phieu_luong</c> chỉ cho đọc phiếu của mình;
/// <c>doc_chi_tiet_phieu_luong</c> nối vòng qua phiếu để lọc đúng chủ phiếu.
/// Giống lịch phân ca, hợp đồng này không nhận tham số mã nhân viên — luôn
/// kẹp theo phiên đăng nhập, nên không có đường nào đọc lương của người khác.
/// </remarks>
public interface IPayslipService
{
    bool IsDemoMode { get; }

    /// <summary>Lấy toàn bộ phiếu lương của người đang đăng nhập, mới nhất trước.</summary>
    Task<IReadOnlyList<PayslipSummaryDto>> GetPayslipsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lấy các dòng thu chi của một phiếu lương.</summary>
    Task<IReadOnlyList<PayslipItemDto>> GetPayslipItemsAsync(int maPhieuLuong,
        CancellationToken cancellationToken = default);
}