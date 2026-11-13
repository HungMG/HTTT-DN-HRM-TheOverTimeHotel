using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.Data.Services;

public interface IPayrollCalculationService
{
    Task<KyLuong> GetOrCreatePeriodAsync(int thang, int nam, CancellationToken cancellationToken = default);
    Task<KyLuong?> GetPeriodWithDetailsAsync(int thang, int nam, CancellationToken cancellationToken = default);
    Task<List<KyLuong>> GetAllPeriodsAsync(CancellationToken cancellationToken = default);
    Task<PhieuLuong?> GetPayslipDetailAsync(int maPhieuLuong, CancellationToken cancellationToken = default);

    Task<KyLuong> CalculatePayrollAsync(int thang, int nam, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> RecalculateEmployeeAsync(int maPhieuLuong, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);

    Task<bool> AdjustSalaryItemAsync(int maPhieuLuong, string loaiKhoan, string tenKhoan, decimal soTien, string? ghiChu, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> DeleteSalaryItemAsync(int maChiTiet, string? nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);

    Task<bool> PublishDraftPayslipsAsync(int maKyLuong, DateTimeOffset hanPhanHoi, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> SubmitForApprovalAsync(int maKyLuong, string nguoiGui, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> ApprovePayrollAsync(int maKyLuong, string nguoiDuyet, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> RejectPayrollAsync(int maKyLuong, string nguoiTuChoi, string lyDo, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> LockPayrollAsync(int maKyLuong, string nguoiKhoa, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> PublishPayslipsAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);
    Task<bool> PublishDirectlyAsync(int maKyLuong, string nguoiThucHien, int? maTaiKhoan, CancellationToken cancellationToken = default);

    Task<(string FileName, byte[] Content)> ExportBankTransferFileAsync(int maKyLuong, CancellationToken cancellationToken = default);
}
