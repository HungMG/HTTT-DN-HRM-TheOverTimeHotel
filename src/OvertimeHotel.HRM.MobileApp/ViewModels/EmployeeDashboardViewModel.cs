using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OvertimeHotel.HRM.MobileApp.Services;
using Timer = System.Timers.Timer;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel của khối Nhân viên: đồng hồ thời gian thật và bản đồ vị trí quẹt thẻ.
/// Đồng hồ chạy mỗi giây (timer nhẹ), GPS làm mới mỗi 30 giây để đủ chứng minh
/// vị trí mà không làm hao pin trong ca làm việc dài.
/// </summary>
public class EmployeeDashboardViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>Chu kỳ làm mới toạ độ GPS (30 giây).</summary>
    private const int GPS_REFRESH_SECONDS = 30;

    private readonly IGeoLocationService _geoLocationService;

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

    public EmployeeDashboardViewModel(IGeoLocationService geoLocationService)
    {
        _geoLocationService = geoLocationService ?? throw new ArgumentNullException(nameof(geoLocationService));

        _clockTimer = new Timer { Interval = 1000 };
        _clockTimer.Elapsed += ClockTimer_Elapsed;

        _gpsTimer = new Timer { Interval = GPS_REFRESH_SECONDS * 1000 };
        _gpsTimer.Elapsed += GpsTimer_Elapsed;

        UpdateClock();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Thuộc tính hiển thị

    /// <summary>Đồng hồ thời gian thật định dạng HH:mm:ss.</summary>
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

        RealTimeText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        RealDateText = string.Create(
            CultureInfo.InvariantCulture,
            $"{VietnameseWeekday(now)}, {now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");

        if (_gpsTimer.Enabled && _secondsUntilGpsRefresh > 0)
        {
            _secondsUntilGpsRefresh--;
            OnPropertyChanged(nameof(GpsRefreshLabel));
        }
    }

    /// <summary>
    /// Tên thứ tiếng Việt. Không dùng "dddd" vì định dạng đó phụ thuộc locale của
    /// thiết bị — máy đang để tiếng Anh sẽ hiện "Sunday" xen giữa giao diện
    /// tiếng Việt. Ở đây ánh xạ thủ công nên luôn cho kết quả nhất quán.
    /// </summary>
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
            var raw = await _geoLocationService.GetCurrentCoordinateAsync();

            if (string.IsNullOrWhiteSpace(raw))
            {
                SetHasLocation(false);
                GpsCoordinateText = "Không lấy được vị trí — hãy bật GPS";
                return;
            }

            var parts = raw.Split(',');
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            {
                SetHasLocation(false);
                GpsCoordinateText = "Toạ độ không hợp lệ";
                return;
            }

            // Phải đi qua property để phát PropertyChanged — gán thẳng vào field
            // sẽ khiến bản đồ không nhận được toạ độ mới ở các vòng làm mới sau.
            Latitude = lat;
            Longitude = lng;
            SetHasLocation(true);
            _secondsUntilGpsRefresh = GPS_REFRESH_SECONDS;

            GpsCoordinateText = string.Create(
                CultureInfo.InvariantCulture,
                $"{lat:F4}, {lng:F4}");
        }
        finally
        {
            _isGpsBusy = false;
            OnPropertyChanged(nameof(IsGpsBusy));
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