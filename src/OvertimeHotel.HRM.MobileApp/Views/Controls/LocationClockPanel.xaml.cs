using System.ComponentModel;
using System.Globalization;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Khối hiển thị đồng hồ thời gian thật và bản đồ vị trí GPS của nhân viên (OpenStreetMap / Leaflet).
/// Tự động đồng bộ vị trí khách sạn và bán kính chấm công do IT cập nhật trên WebAdmin.
/// </summary>
public partial class LocationClockPanel : ContentView
{
    private EmployeeDashboardViewModel? _viewModel;
    private double _lastUserLat;
    private double _lastUserLng;
    private double _lastHotelLat;
    private double _lastHotelLng;
    private double _lastHotelRadius;
    private bool _isMapInitialized;

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
            || e.PropertyName == nameof(EmployeeDashboardViewModel.HotelLatitude)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.HotelLongitude)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.HotelRadiusMeters)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.HotelName)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.DistanceToHotelMeters)
            || e.PropertyName == nameof(EmployeeDashboardViewModel.IsGpsLoading))
        {
            UpdateMap();
        }
    }

    /// <summary>
    /// Cập nhật vị trí trên bản đồ. Luôn chạy trên MainThread để đảm bảo an toàn giao diện.
    /// </summary>
    private void UpdateMap()
    {
        if (MainThread.IsMainThread)
        {
            UpdateMapCore();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(UpdateMapCore);
        }
    }

    private async void UpdateMapCore()
    {
        if (_viewModel is null)
        {
            return;
        }

        gpsWaitingOverlay.IsVisible = !_viewModel.HasLocation;

        if (!_viewModel.HasLocation)
        {
            return;
        }

        var userLat = _viewModel.Latitude;
        var userLng = _viewModel.Longitude;
        var hotelLat = _viewModel.HotelLatitude;
        var hotelLng = _viewModel.HotelLongitude;
        var hotelRadius = _viewModel.HotelRadiusMeters;
        var hotelName = _viewModel.HotelName;
        var dist = _viewModel.DistanceToHotelMeters;
        var distText = dist.HasValue
            ? (dist.Value >= 1000 ? $"{(dist.Value / 1000):F1} km" : $"{dist.Value:F0} m")
            : "--";

        bool userMoved = Math.Abs(_lastUserLat - userLat) > 0.00001 || Math.Abs(_lastUserLng - userLng) > 0.00001;
        bool hotelMoved = Math.Abs(_lastHotelLat - hotelLat) > 0.00001 || Math.Abs(_lastHotelLng - hotelLng) > 0.00001 || Math.Abs(_lastHotelRadius - hotelRadius) > 0.5;

        if (_isMapInitialized && !userMoved && !hotelMoved)
        {
            return;
        }

        _lastUserLat = userLat;
        _lastUserLng = userLng;
        _lastHotelLat = hotelLat;
        _lastHotelLng = hotelLng;
        _lastHotelRadius = hotelRadius;

        if (!_isMapInitialized)
        {
            _isMapInitialized = true;
            var html = BuildMapHtml(userLat, userLng, hotelLat, hotelLng, hotelRadius, hotelName, distText, dist ?? 99999);
            mapWebView.Source = new HtmlWebViewSource { Html = html };
        }
        else
        {
            try
            {
                var uLat = userLat.ToString("F6", CultureInfo.InvariantCulture);
                var uLng = userLng.ToString("F6", CultureInfo.InvariantCulture);
                var hLat = hotelLat.ToString("F6", CultureInfo.InvariantCulture);
                var hLng = hotelLng.ToString("F6", CultureInfo.InvariantCulture);
                var r = hotelRadius.ToString("F1", CultureInfo.InvariantCulture);
                var nameEscaped = hotelName.Replace("'", "\\'");

                await mapWebView.EvaluateJavaScriptAsync(
                    $"if (window.updatePositions) {{ window.updatePositions({uLat}, {uLng}, {hLat}, {hLng}, {r}, '{nameEscaped}', '{distText}'); }}");
            }
            catch
            {
                var html = BuildMapHtml(userLat, userLng, hotelLat, hotelLng, hotelRadius, hotelName, distText, dist ?? 99999);
                mapWebView.Source = new HtmlWebViewSource { Html = html };
            }
        }
    }

    private static string BuildMapHtml(
        double userLat, double userLng,
        double hotelLat, double hotelLng,
        double radius, string hotelName,
        string distText, double distMetersRaw)
    {
        var userLatStr = userLat.ToString("F6", CultureInfo.InvariantCulture);
        var userLngStr = userLng.ToString("F6", CultureInfo.InvariantCulture);
        var hotelLatStr = hotelLat.ToString("F6", CultureInfo.InvariantCulture);
        var hotelLngStr = hotelLng.ToString("F6", CultureInfo.InvariantCulture);
        var radiusStr = radius.ToString("F1", CultureInfo.InvariantCulture);
        var distMetersStr = distMetersRaw.ToString("F0", CultureInfo.InvariantCulture);
        var hotelNameEscaped = hotelName.Replace("'", "\\'");

        return $$"""
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <style>
        html, body { margin: 0; padding: 0; width: 100%; height: 100%; background: #F1F5F9; overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
        #map { width: 100%; height: 100%; }
        .map-controls {
            position: absolute;
            top: 8px;
            right: 8px;
            z-index: 1000;
            display: flex;
            flex-direction: column;
            gap: 4px;
        }
        .ctrl-btn {
            background: #FFFFFF;
            color: #1E3A8A;
            border: 1px solid #CBD5E1;
            border-radius: 6px;
            padding: 5px 8px;
            font-size: 11px;
            font-weight: bold;
            box-shadow: 0 1px 3px rgba(0,0,0,0.18);
            cursor: pointer;
            text-align: center;
            user-select: none;
        }
        .zoom-btn {
            width: 28px;
            height: 28px;
            font-size: 16px;
            padding: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            align-self: flex-end;
        }
        .ctrl-btn:active {
            background: #E2E8F0;
        }
        .pulse-dot {
            width: 14px;
            height: 14px;
            background: #2563EB;
            border: 2.5px solid #FFFFFF;
            border-radius: 50%;
            box-shadow: 0 0 6px rgba(37,99,235,0.7);
            position: relative;
        }
        .pulse-ring {
            border: 2px solid #3B82F6;
            border-radius: 50%;
            height: 32px;
            width: 32px;
            position: absolute;
            left: -11px;
            top: -11px;
            animation: pulsate 1.8s ease-out infinite;
            opacity: 0;
        }
        @keyframes pulsate {
            0% { transform: scale(0.3); opacity: 0; }
            50% { opacity: 0.9; }
            100% { transform: scale(1.3); opacity: 0; }
        }
        .hotel-pin {
            background: #1E3A8A;
            color: #FDE047;
            font-size: 14px;
            font-weight: bold;
            width: 28px;
            height: 28px;
            border-radius: 50%;
            border: 2px solid #FFFFFF;
            box-shadow: 0 2px 6px rgba(0,0,0,0.35);
            display: flex;
            align-items: center;
            justify-content: center;
        }
    </style>
</head>
<body>
    <div id="map"></div>
    <div class="map-controls">
        <div class="ctrl-btn" onclick="flyToUser()">⌖ Tôi</div>
        <div class="ctrl-btn" onclick="flyToHotel()">🏨 KS</div>
        <div class="ctrl-btn" onclick="fitBoth()">🗺️ Cả hai</div>
        <div class="ctrl-btn zoom-btn" onclick="map.zoomIn()">+</div>
        <div class="ctrl-btn zoom-btn" onclick="map.zoomOut()">−</div>
    </div>
    <script>
        var map = L.map('map', { zoomControl: false, attributionControl: false }).setView([{{userLatStr}}, {{userLngStr}}], 16);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(map);

        // Marker khách sạn (vị trí do IT cập nhật trên WebAdmin)
        var hotelIcon = L.divIcon({
            className: 'custom-hotel-icon',
            html: '<div class="hotel-pin">★</div>',
            iconSize: [28, 28],
            iconAnchor: [14, 14]
        });
        window.hotelMarker = L.marker([{{hotelLatStr}}, {{hotelLngStr}}], { icon: hotelIcon }).addTo(map);
        window.hotelMarker.bindPopup('<b>{{hotelNameEscaped}}</b><br>Khu vực chấm công (IT cấu hình)<br>Bán kính: {{radiusStr}} m');

        // Vòng tròn bán kính Geofence
        window.geofenceCircle = L.circle([{{hotelLatStr}}, {{hotelLngStr}}], {
            color: '#1E3A8A',
            dashArray: '5, 5',
            fillColor: '#3B82F6',
            fillOpacity: 0.15,
            radius: {{radiusStr}}
        }).addTo(map);

        // Marker vị trí người dùng
        var userIcon = L.divIcon({
            className: 'custom-user-icon',
            html: '<div class="pulse-ring"></div><div class="pulse-dot"></div>',
            iconSize: [20, 20],
            iconAnchor: [10, 10]
        });
        window.userMarker = L.marker([{{userLatStr}}, {{userLngStr}}], { icon: userIcon }).addTo(map);
        window.userMarker.bindPopup('<b>Vị trí hiện tại của bạn</b><br>Toạ độ: {{userLatStr}}, {{userLngStr}}<br>Cách khách sạn: {{distText}}');

        function flyToUser() {
            if (window.userMarker && window.map) {
                window.map.flyTo(window.userMarker.getLatLng(), 17);
                window.userMarker.openPopup();
            }
        }
        function flyToHotel() {
            if (window.hotelMarker && window.map) {
                window.map.flyTo(window.hotelMarker.getLatLng(), 17);
                window.hotelMarker.openPopup();
            }
        }
        function fitBoth() {
            if (window.userMarker && window.hotelMarker && window.map) {
                var group = new L.featureGroup([window.userMarker, window.hotelMarker]);
                window.map.fitBounds(group.getBounds().pad(0.25));
            }
        }

        window.updatePositions = function(userLat, userLng, hotelLat, hotelLng, radius, name, distText) {
            if (window.userMarker) {
                window.userMarker.setLatLng([userLat, userLng]);
                window.userMarker.setPopupContent('<b>Vị trí hiện tại của bạn</b><br>Toạ độ: ' + userLat.toFixed(6) + ', ' + userLng.toFixed(6) + '<br>Cách khách sạn: ' + distText);
            }
            if (window.hotelMarker) {
                window.hotelMarker.setLatLng([hotelLat, hotelLng]);
                window.hotelMarker.setPopupContent('<b>' + name + '</b><br>Khu vực chấm công (IT cấu hình)<br>Bán kính: ' + radius + ' m');
            }
            if (window.geofenceCircle) {
                window.geofenceCircle.setLatLng([hotelLat, hotelLng]);
                window.geofenceCircle.setRadius(radius);
            }
        };

        if ({{distMetersStr}} <= 1500) {
            fitBoth();
        } else {
            window.userMarker.openPopup();
        }
    </script>
</body>
</html>
""";
    }
}
