using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace OvertimeHotel.HRM.MobileApp.Models;

/// <summary>
/// Model đại diện cho từng chip bộ phận khách sạn trong thanh lọc nhanh của HR.
/// Tự động cập nhật màu nền và chữ khi được chọn.
/// </summary>
public class DepartmentFilterChip : INotifyPropertyChanged
{
	public string Name { get; set; } = string.Empty;

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
				OnPropertyChanged(nameof(BorderColor));
				OnPropertyChanged(nameof(BorderWidth));
				OnPropertyChanged(nameof(FontFamily));
			}
		}
	}

	public Color BackgroundColor => IsSelected ? Color.FromArgb("#1E3A8A") : Color.FromArgb("#FFFFFF");
	public Color TextColor => IsSelected ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#1E3A8A");
	public Color BorderColor => IsSelected ? Color.FromArgb("#1E3A8A") : Color.FromArgb("#CBD5E1");
	public double BorderWidth => IsSelected ? 0 : 1;
	public string FontFamily => IsSelected ? "SegoeUIBold" : "SegoeUI";

	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
