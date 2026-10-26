using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Một nút trong thanh lọc trạng thái chấm công của tháng (Tất cả / Đúng giờ / Trễ, sớm / Vắng).
/// Tự đổi màu nền, màu chữ và số đếm khi được chọn — cùng khuôn với
/// <see cref="DepartmentFilterChip"/> để giao diện toàn app nhìn thống nhất.
/// </summary>
public class AttendanceStatusFilter : INotifyPropertyChanged
{
    /// <summary>Mã lọc, khớp với <c>AttendanceViewModel.FilterCommand</c> khi bấm.</summary>
    public string Code { get; set; } = "ALL";

    /// <summary>Nhãn hiển thị trên nút.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Vị trí cột trong Grid 4 cột. BindableLayout chỉ chèn con vào Grid theo thứ tự
    /// chứ không tự chia cột, nên mỗi nút tự khai báo cột của mình.
    /// </summary>
    public int ColumnIndex { get; set; }

    private int _count;
    /// <summary>Số ngày khớp bộ lọc trong tháng đang xem.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(TextColor));
                OnPropertyChanged(nameof(CountColor));
                OnPropertyChanged(nameof(BorderColor));
                OnPropertyChanged(nameof(BorderWidth));
                OnPropertyChanged(nameof(NameFontFamily));
            }
        }
    }

    public Color BackgroundColor => IsSelected ? Color.FromArgb("#1E3A8A") : Color.FromArgb("#FFFFFF");
    public Color TextColor => IsSelected ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#1E3A8A");
    public Color CountColor => IsSelected ? Color.FromArgb("#BFDBFE") : Color.FromArgb("#94A3B8");
    public Color BorderColor => IsSelected ? Color.FromArgb("#1E3A8A") : Color.FromArgb("#CBD5E1");
    public double BorderWidth => IsSelected ? 0 : 1;
    public string NameFontFamily => IsSelected ? "SegoeUIBold" : "SegoeUI";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
