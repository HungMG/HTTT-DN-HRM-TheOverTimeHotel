using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Khối hiển thị đồng hồ thời gian thật và bản đồ vị trí GPS của nhân viên.
/// </summary>
public partial class LocationClockPanel : ContentView
{
    /// <summary>Bán kính hiển thị ban đầu (mét) — đủ gần để thấy khuôn viên khách sạn.</summary>
    private const double DefaultSpanMeters = 350;

    private EmployeeDashboardViewModel? _viewModel;
    private Pin? _currentPin;

    public LocationClockPanel()
    {
        InitializeComponent();
    }

    /// <summary>Gắn ViewModel vào khối hiển thị.</summary>
    public void Bind(EmployeeDashboardViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        // Huỷ đăng ký trước để Bind lại không nhân đôi sự kiện.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        BindingContext = _viewModel;

        UpdateMap();
    }

    /// <summary>Ngừng theo dõi ViewModel cũ khi khối hiển thị bị tháo khỏi cây trang.</summary>
    public void Unbind()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EmployeeDashboardViewModel.HasLocation)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.Latitude)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.Longitude)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.IsGpsLoading))
        {
            UpdateMap();
        }
    }

    /// <summary>
    /// Cập nhật vị trí trên bản đồ. Chỉ nhảy camera lần đầu có toạ độ để tránh giật màn hình.
    /// </summary>
    private void UpdateMap()
    {
        if (_viewModel is null)
        {
            return;
        }

        gpsWaitingOverlay.IsVisible = !_viewModel.HasLocation;

        if (!_viewModel.HasLocation)
        {
            // Chưa có toạ độ: bỏ pin cũ (nếu có) để không kẹt ở vị trí lần trước.
            if (_currentPin is not null)
            {
                locationMap.Pins.Remove(_currentPin);
                _currentPin = null;
            }

            return;
        }

        var location = new Location(_viewModel.Latitude, _viewModel.Longitude);

        if (_currentPin is null)
        {
            _currentPin = new Pin
            {
                Type = PinType.Place,
                Label = "Vị trí của bạn",
                Location = location
            };

            locationMap.Pins.Add(_currentPin);
            locationMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, new Distance(DefaultSpanMeters)));
            return;
        }

        _currentPin.Location = location;
    }
}