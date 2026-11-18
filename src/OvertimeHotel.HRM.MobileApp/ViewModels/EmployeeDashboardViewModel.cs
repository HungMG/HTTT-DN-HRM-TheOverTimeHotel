using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.MobileApp.Services;
using Timer = System.Timers.Timer;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel của khối Nhân viên: đồng hồ thời gian thật và bản đồ vị trí quẹt thẻ.
/// Đồng hồ chạy mỗi giây (timer nhẹ), GPS làm mới mỗi 30 giây để đủ chứng minh
/// vị trí mà không làm hao pin trong ca làm việc dài.
/// Tự động đồng bộ vị trí khách sạn và bán kính chấm công do IT cập nhật trên WebAdmin.
/// </summary>
public class EmployeeDashboardViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>Chu kỳ làm mới toạ độ GPS (30 giây).</summary>
    private const int GPS_REFRESH_SECONDS = 30;

    private readonly IGeoLocationService _geoLocationService;
    private readonly SupabaseRestClient? _supabaseRestClient;
    private readonly ILogger<EmployeeDashboardViewModel>? _logger;

    private readonly Timer _clockTimer;
    private readonly Timer _gpsTimer;

    private string _realTimeText = string.Empty;
    private string _realDateText = string.Empty;
    private string _gpsCoordinateText = "Đang xác định vị trí...";
    private double _latitude;
    private double _longitude;
    private bool _hasLocation;
    private bool _isGpsBusy;
    private int _secondsUntilGpsRefresh = GPS_REFRESH_SECONDS;

    // Thông tin khu vực chấm công do IT cấu hình trên WebAdmin
    private string _hotelName = "The OverTime Hotel & Resort";
    private string _hotelAddress = "";
    private double _hotelLatitude = 10.778848d;
    private double _hotelLongitude = 106.687421d;
    private double _hotelRadiusMeters = 250d;
    private double? _distanceToHotelMeters;
    private bool _isWithinGeofence;
    private string _geofenceStatusText = "Đang kiểm tra phạm vi...";

    public EmployeeDashboardViewModel(
        IGeoLocationService geoLocationService,
        SupabaseRestClient? supabaseRestClient = null,
        ILogger<EmployeeDashboardViewModel>? logger = null)
    {
        _geoLocationService = geoLocationService ?? throw new ArgumentNullException(nameof(geoLocationService));
        _supabaseRestClient = supabaseRestClient;
        _logger = logger;

        _clockTimer = new Timer { Interval = 1000 };
        _clockTimer.Elapsed += ClockTimer_Elapsed;

        _gpsTimer = new Timer { Interval = GPS_REFRESH_SECONDS * 1000 };
        _gpsTimer.Elapsed += GpsTimer_Elapsed;

        UpdateClock();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Thuộc tính hiển thị

    /// <summary>Đồng hồ thời gian thật định dạng HH:mm (bỏ giây cho gọn, đủ dùng trong ca).</summary>
    public string RealTimeText
    {
        get => _realTimeText;
        private set => SetProperty(ref _realTimeText, value);
    }

    /// <summary>Ngày tháng thật định dạng "Thứ Bảy, 03/10/2026".</summary>
    public string RealDateText
    {
        get => _realDateText;
        private set => SetProperty(ref _realDateText, value);
    }

    /// <summary>Toạ độ GPS hiển thị, hoặc thông báo khi chưa lấy được.</summary>
    public string GpsCoordinateText
    {
        get => _gpsCoordinateText;
        private set => SetProperty(ref _gpsCoordinateText, value);
    }

    /// <summary>Vĩ độ hiện tại. Chỉ dùng khi <see cref="HasLocation"/> bằng true.</summary>
    public double Latitude
    {
        get => _latitude;
        private set => SetProperty(ref _latitude, value);
    }

    /// <summary>Kinh độ hiện tại. Chỉ dùng khi <see cref="HasLocation"/> bằng true.</summary>
    public double Longitude
    {
        get => _longitude;
        private set => SetProperty(ref _longitude, value);
    }

    /// <summary>
    /// Đã từng lấy được toạ độ — quyết định có hiện vị trí trên bản đồ không.
    /// Cố ý KHÔNG phụ thuộc <see cref="IsGpsBusy"/>: nếu gắn trạng thái đang tải vào
    /// đây thì mỗi vòng làm mới 30 giây sẽ gỡ tạm pin ra khỏi bản đồ rồi gắn lại,
    /// khiến lớp phủ chờ che mất tile và bản đồ nhấp nháy liên tục.
    /// </summary>
    public bool HasLocation => _hasLocation;

    /// <summary>Đang lấy toạ độ lần đầu (chưa có kết quả để hiển thị).</summary>
    public bool IsGpsLoading => _isGpsBusy && !_hasLocation;

    /// <summary>Đang lấy toạ độ.</summary>
    public bool IsGpsBusy
    {
        get => _isGpsBusy;
        private set
        {
            if (SetProperty(ref _isGpsBusy, value))
            {
                OnPropertyChanged(nameof(IsGpsLoading));
            }
        }
    }

    /// <summary>
    /// Cờ có toạ độ — gán qua helper để mọi lần đổi đều phát <see cref="OnPropertyChanged"/>,
    /// giúp lớp phủ chờ và lớp gạch chân trên bản đồ cập nhật theo.
    /// </summary>
    private void SetHasLocation(bool value)
    {
        if (SetProperty(ref _hasLocation, value))
        {
            OnPropertyChanged(nameof(HasLocation));
        }
    }

    /// <summary>Nhãn làm mới GPS kèm số giây đếm ngược.</summary>
    public string GpsRefreshLabel =>
        $"↻ làm mới GPS sau {Math.Max(0, _secondsUntilGpsRefresh)}s";

    #region Thuộc tính vị trí khách sạn (IT cấu hình trên WebAdmin)

    public string HotelName
    {
        get => _hotelName;
        private set => SetProperty(ref _hotelName, value);
    }

    public string HotelAddress
    {
        get => _hotelAddress;
        private set => SetProperty(ref _hotelAddress, value);
    }

    public double HotelLatitude
    {
        get => _hotelLatitude;
        private set
        {
            if (SetProperty(ref _hotelLatitude, value))
            {
                OnPropertyChanged(nameof(HotelCoordinateText));
                OnPropertyChanged(nameof(HotelSummaryText));
            }
        }
    }

    public double HotelLongitude
    {
        get => _hotelLongitude;
        private set
        {
            if (SetProperty(ref _hotelLongitude, value))
            {
                OnPropertyChanged(nameof(HotelCoordinateText));
                OnPropertyChanged(nameof(HotelSummaryText));
            }
        }
    }

    public double HotelRadiusMeters
    {
        get => _hotelRadiusMeters;
        private set
        {
            if (SetProperty(ref _hotelRadiusMeters, value))
            {
                OnPropertyChanged(nameof(HotelSummaryText));
            }
        }
    }

    public string HotelCoordinateText => $"{_hotelLatitude:F6}, {_hotelLongitude:F6}";

    public string HotelSummaryText => $"{_hotelName} · R={_hotelRadiusMeters:F0}m";

    public double? DistanceToHotelMeters
    {
        get => _distanceToHotelMeters;
        private set => SetProperty(ref _distanceToHotelMeters, value);
    }

    public bool IsWithinGeofence
    {
        get => _isWithinGeofence;
        private set
        {
            if (SetProperty(ref _isWithinGeofence, value))
            {
                OnPropertyChanged(nameof(GeofenceStatusColor));
            }
        }
    }

    public string GeofenceStatusText
    {
        get => _geofenceStatusText;
        private set => SetProperty(ref _geofenceStatusText, value);
    }

    public string GeofenceStatusColor => _isWithinGeofence ? "#059669" : "#DC2626";

    #endregion

    #endregion

    #region Vòng đời

    /// <summary>Bắt đầu chạy đồng hồ và nạp toạ độ lần đầu.</summary>
    public async Task StartAsync()
    {
        _clockTimer.Start();
        _gpsTimer.Start();
        _secondsUntilGpsRefresh = GPS_REFRESH_SECONDS;
        OnPropertyChanged(nameof(GpsRefreshLabel));

        await RefreshLocationAsync();
    }

    /// <summary>Dừng timer khi rời màn hình để không tiêu hao pin vô ích.</summary>
    public void Stop()
    {
        _clockTimer.Stop();
        _gpsTimer.Stop();
    }

    public void Dispose()
    {
        _clockTimer.Elapsed -= ClockTimer_Elapsed;
        _gpsTimer.Elapsed -= GpsTimer_Elapsed;
        _clockTimer.Dispose();
        _gpsTimer.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion

    #region Xử lý sự kiện timer

    private void ClockTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // System.Timers.Timer phát sự kiện trên thread pool, phải đưa cập nhật về UI thread.
        MainThread.BeginInvokeOnMainThread(UpdateClock);
    }

    private void GpsTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await RefreshLocationAsync());

    /// <summary>
    /// Cập nhật đồng hồ thật và đếm ngược tới lần làm mới GPS kế tiếp.
    /// </summary>
    private void UpdateClock()
    {
        var now = DateTime.Now;

        RealTimeText = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        RealDateText = string.Create(
            CultureInfo.InvariantCulture,
            $"{VietnameseWeekday(now)}, {now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");

        if (_gpsTimer.Enabled && _secondsUntilGpsRefresh > 0)
        {
            _secondsUntilGpsRefresh--;
            OnPropertyChanged(nameof(GpsRefreshLabel));
        }
    }

    private static string VietnameseWeekday(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        _ => "Chủ Nhật"
    };

    private async Task RefreshLocationAsync()
    {
        if (_isGpsBusy)
        {
            return;
        }

        _isGpsBusy = true;
        OnPropertyChanged(nameof(IsGpsBusy));

        try
        {
            // 1. Tải cấu hình toạ độ mới nhất từ Supabase Cloud (do IT cập nhật trên WebAdmin)
            if (_logger != null)
            {
                var config = await GeofenceConfig.LoadAsync(_logger, _supabaseRestClient, force: true);
                HotelName = config.HotelName;
                HotelAddress = config.Address;
                HotelLatitude = config.HotelLatitude;
                HotelLongitude = config.HotelLongitude;
                HotelRadiusMeters = config.RadiusMeters;
            }

            // 2. Lấy toạ độ GPS chính xác từ thiết bị
            var raw = await _geoLocationService.GetCurrentCoordinateAsync();

            if (string.IsNullOrWhiteSpace(raw))
            {
                SetHasLocation(false);
                GpsCoordinateText = "Không lấy được vị trí — hãy bật GPS";
                GeofenceStatusText = "Chưa nhận được tín hiệu GPS";
                return;
            }

            var parts = raw.Split(',');
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            {
                SetHasLocation(false);
                GpsCoordinateText = "Toạ độ không hợp lệ";
                GeofenceStatusText = "Toạ độ GPS không hợp lệ";
                return;
            }

            Latitude = lat;
            Longitude = lng;
            SetHasLocation(true);
            _secondsUntilGpsRefresh = GPS_REFRESH_SECONDS;

            GpsCoordinateText = string.Create(
                CultureInfo.InvariantCulture,
                $"{lat:F6}, {lng:F6}");

            // 3. Tính khoảng cách tới toạ độ khách sạn thực tế từ IT
            var dist = GeofencePolicy.CalculateDistanceMeters(lat, lng, HotelLatitude, HotelLongitude);
            DistanceToHotelMeters = dist;
            IsWithinGeofence = dist <= HotelRadiusMeters;

            if (IsWithinGeofence)
            {
                GeofenceStatusText = $"✓ Trong phạm vi chấm công (cách {dist:F0}m / {HotelRadiusMeters:F0}m)";
            }
            else
            {
                var distStr = dist >= 1000 ? $"{(dist / 1000):F1} km" : $"{dist:F0} m";
                GeofenceStatusText = $"✕ Ngoài phạm vi (cách {distStr} / bán kính {HotelRadiusMeters:F0}m)";
            }
        }
        finally
        {
            _isGpsBusy = false;
            OnPropertyChanged(nameof(IsGpsBusy));
            OnPropertyChanged(nameof(IsGpsLoading));
            OnPropertyChanged(nameof(GpsRefreshLabel));
        }
    }

    #endregion

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
