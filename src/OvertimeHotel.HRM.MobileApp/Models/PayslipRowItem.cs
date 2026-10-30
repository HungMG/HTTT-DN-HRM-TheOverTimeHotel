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

    public string SlipIdText => $"#{slip.MaPhieuLuong:D4}";

    // Kỳ lương đọc qua quan hệ ky_luong. Nếu policy trên Cloud chưa mở (thiếu
    // migration 20261010) thì embed trả null và Thang/Nam bằng 0 — lúc đó hiện
    // mã kỳ để người dùng vẫn phân biệt được các phiếu, thay vì dòng trắng.
    public string PeriodText => slip.Thang > 0 && slip.Nam > 0
        ? $"Tháng {slip.Thang:00} / {slip.Nam:0000}"
        : $"Kỳ lương #{slip.MaPhieuLuong}";

    public string NetText => $"{Format(slip.ThucNhan)} ₫";

    public string EarnedText => $"{Format(slip.TienLuongCong + slip.TienTangCa + slip.TongPhuCap + slip.TongThuong)} ₫";

    public string DeductionText => $"{Format(slip.TongKhauTru)} ₫";

    public string WorkDayText => $"{slip.NgayCongThucTe:0.##} / 26 ngày công";

    public string BaseSalaryText => $"{Format(slip.LuongCoBan)} ₫";

    public string StatusText => slip.TrangThai switch
    {
        "DA_DUYET" => "Đã khóa sổ",
        "DA_THANH_TOAN" => "Đã gửi · Đã chốt",
        _ => "Đang xử lý"
    };

    public string StatusColor => slip.TrangThai switch
    {
        "DA_DUYET" => "#047857",
        "DA_THANH_TOAN" => "#047857",
        _ => "#64748B"
    };

    public string StatusBackground => slip.TrangThai switch
    {
        "DA_DUYET" => "#ECFDF5",
        "DA_THANH_TOAN" => "#ECFDF5",
        _ => "#F1F5F9"
    };

    public static string Format(decimal amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>Một dòng thu chi trong phần chi tiết phiếu lương.</summary>
public class PayslipItemRowItem(PayslipItemDto item)
{
    public int MaChiTiet => item.MaChiTiet;
    public string Name => item.TenKhoan;
    public string LoaiKhoan => item.LoaiKhoan;
    public decimal SoTien => item.SoTien;
    public decimal RawAmount => Math.Abs(item.SoTien);

    public bool IsDeduction => string.Equals(item.LoaiKhoan, "KHAU_TRU", StringComparison.OrdinalIgnoreCase);
    public bool IsIncome => !IsDeduction;

    public string AmountText => (IsDeduction ? "- " : "+ ") + PayslipRowItem.Format(RawAmount) + " ₫";

    public string AmountColor => IsDeduction ? "#DC2626" : "#059669";

    public string Note => item.GhiChu ?? string.Empty;
    public bool HasNote => !string.IsNullOrWhiteSpace(item.GhiChu);

    public string BadgeText => item.LoaiKhoan switch
    {
        "KHAU_TRU" => "Khấu trừ",
        "TANG_CA" => "Tăng ca OT",
        "PHU_CAP" => "Phụ cấp",
        "THUONG" => "Tiền thưởng",
        _ => "Khoản mục"
    };

    public string BadgeTextColor => item.LoaiKhoan switch
    {
        "KHAU_TRU" => "#DC2626",
        "TANG_CA" => "#1D4ED8",
        "THUONG" => "#B45309",
        _ => "#047857"
    };

    public string BadgeBackgroundColor => item.LoaiKhoan switch
    {
        "KHAU_TRU" => "#FEF2F2",
        "TANG_CA" => "#EFF6FF",
        "THUONG" => "#FFFBEB",
        _ => "#ECFDF5"
    };
}
