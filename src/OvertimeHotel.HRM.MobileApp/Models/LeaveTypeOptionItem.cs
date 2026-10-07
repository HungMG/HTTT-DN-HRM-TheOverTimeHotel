using System.ComponentModel;

namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Một loại đơn người dùng được chọn khi nộp, bọc lại
/// <see cref="Core.DTOs.LeaveTypeOption"/> cho XAML.
///
/// <para>
/// Đồng dạng với <see cref="LeaveRequestItem"/>: chuỗi hiển thị và màu badge tính
/// sẵn trong model thay vì dùng IValueConverter — dự án này không có converter nào,
/// đó là quy ước có chủ đích chứ không phải thiếu sót.
/// </para>
/// </summary>
public class LeaveTypeOptionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public int MaLoaiDon { get; set; }

    public string TenLoaiDon { get; set; } = string.Empty;

    public bool CoHuongLuong { get; set; }

    public string MoTa { get; set; } = string.Empty;

    /// <summary>
    /// Loại này đang được chọn hay không.
    /// </summary>
    /// <remarks>
    /// ViewModel tự bật/tắt cờ này cho mọi item thay vì viết năm DataTrigger so
    /// sánh mã loại trong XAML — vừa ngắn hơn, vừa không phải lo chuyển đổi kiểu
    /// giữa <c>Value="1"</c> (chuỗi) và binding kiểu <c>int</c> của DataTrigger.
    /// </remarks>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Số thứ tự hiển thị, ví dụ "Loại 1 · Nghỉ phép năm".</summary>
    public string TitleDisplay => $"Loại {MaLoaiDon} · {TenLoaiDon}";

    /// <summary>
    /// Nhãn rút gọn cho chip ngang: chỉ lấy phần tên, bỏ chữ "Loại N" vì số
    /// thứ tự chỉ đáng để nhìn ở dạng dọc, ở dải ngang sẽ làm chip bị rộng vô lý.
    /// </summary>
    public string ShortName => TenLoaiDon;

    /// <summary>Chữ trên chip hưởng lương.</summary>
    public string SalaryBadgeText => CoHuongLuong ? "Có hưởng lương" : "Không hưởng lương";

    /// <summary>Viền đậm hơn khi đang chọn, để mắt dễ bám theo lựa chọn hiện tại.</summary>
    public double BorderThicknessDisplay => IsSelected ? 3 : 1.5;

    // Bảng màu khớp 1-1 với LeaveRequestItem.TypeBgColor/TypeColor để loại đơn
    // hiện ở form và ở danh sách lịch sử trông như cùng một hệ màu.
    public string TypeBgColor => MaLoaiDon switch
    {
        1 => "#ECFDF5", // Phép năm (Emerald-50)
        2 => "#FFF7ED", // Nghỉ ốm (Orange-50)
        3 => "#EEF2FF", // Thai sản (Indigo-50)
        4 => "#F1F5F9", // Việc riêng (Slate-100)
        5 => "#FFF1F2", // Thôi việc (Rose-50)
        _ => "#F8FAFC"
    };

    public string TypeColor => MaLoaiDon switch
    {
        1 => "#047857", // Emerald-700
        2 => "#C2410C", // Orange-700
        3 => "#4338CA", // Indigo-700
        4 => "#475569", // Slate-600
        5 => "#BE123C", // Rose-700
        _ => "#334155"
    };
}