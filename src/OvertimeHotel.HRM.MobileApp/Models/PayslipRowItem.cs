using System.Globalization;
using OvertimeHotel.HRM.Core.DTOs;

namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>Một dòng phiếu lương trong danh sách.</summary>
public class PayslipRowItem(PayslipSummaryDto slip)
{
    /// <summary>
    /// DTO gốc. ViewModel giữ dòng đang xem chi tiết qua property này, thay vì
    /// dựng lại DTO từ chuỗi đã định dạng — làm vậy sẽ mất số tiền gốc.
    /// </summary>
    public PayslipSummaryDto Source => slip;

    public int MaPhieuLuong => slip.MaPhieuLuong;

    // Kỳ lương đọc qua quan hệ ky_luong. Nếu policy trên Cloud chưa mở (thiếu
    // migration 20261010) thì embed trả null và Thang/Nam bằng 0 — lúc đó hiện
    // mã kỳ để người dùng vẫn phân biệt được các phiếu, thay vì dòng trắng.
    public string PeriodText => slip.Thang > 0 && slip.Nam > 0
        ? $"Tháng {slip.Thang:00} / {slip.Nam:0000}"
        : $"Kỳ lương #{slip.MaPhieuLuong}";

    public string NetText => Format(slip.ThucNhan);

    public string EarnedText => Format(slip.TienLuongCong + slip.TienTangCa + slip.TongPhuCap + slip.TongThuong);

    public string DeductionText => Format(slip.TongKhauTru);

    public string WorkDayText => $"{slip.NgayCongThucTe:0.##} ngày công";

    public string BaseSalaryText => Format(slip.LuongCoBan);

    public string StatusText => slip.TrangThai switch
    {
        "DA_DUYET" => "Đã duyệt",
        "DA_THANH_TOAN" => "Đã thanh toán",
        _ => "Đang nhập"
    };

    public string StatusColor => slip.TrangThai switch
    {
        "DA_DUYET" => "#1E3A8A",
        "DA_THANH_TOAN" => "#047857",
        _ => "#B45309"
    };

    public string StatusBackground => slip.TrangThai switch
    {
        "DA_DUYET" => "#EFF6FF",
        "DA_THANH_TOAN" => "#ECFDF5",
        _ => "#FFFBEB"
    };

    public static string Format(decimal amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>Một dòng thu chi trong phần chi tiết phiếu lương.</summary>
public class PayslipItemRowItem(PayslipItemDto item)
{
    public string Name => item.TenKhoan;

    // Dòng KHAU_TRU đã mang giá trị âm sẵn trong CSDL (ví dụ -892500.00), nên
    // hiển thị kèm dấu trừ và không trừ thêm lần nữa ở tầng UI.
    public string AmountText => PayslipRowItem.Format(item.SoTien);

    public bool IsDeduction => item.LoaiKhoan == "KHAU_TRU";

    public string AmountColor => IsDeduction ? "#B91C1C" : "#047857";

    public string Note => item.GhiChu ?? string.Empty;
    public bool HasNote => !string.IsNullOrWhiteSpace(item.GhiChu);
}