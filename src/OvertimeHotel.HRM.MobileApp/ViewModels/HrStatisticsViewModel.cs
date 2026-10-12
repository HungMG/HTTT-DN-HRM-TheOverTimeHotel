using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

/// <summary>
/// ViewModel cho Trang Thống Kê Nhân Sự (Mở từ lối tắt trên Trang Chủ HR).
/// Thiết kế bám sát Mục 5 của tài liệu design_ui_ux.md.
/// </summary>
public class HrStatisticsViewModel : INotifyPropertyChanged
{
    private readonly IAuthService _authService;
    private string _selectedPeriod = "Tháng 10/2026";
    private string _lastUpdatedText = "Cập nhật lúc 08:30";
    private bool _isRefreshing;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    // 1. Tổng nhân viên
    public int TotalEmployees => 142;
    public int OfficialEmployees => 128;
    public int ProbationEmployees => 14;

    // 2. Cơ cấu phòng ban
    public string FrontOfficeCount => "28";
    public string HousekeepingCount => "45";
    public string FoodAndBeverageCount => "35";
    public string EngineeringCount => "14";
    public string SecurityCount => "12";
    public string BackOfficeCount => "8";

    // 3. Trình độ học vấn
    public string UniversityRate => "42%";
    public string CollegeRate => "38%";
    public string OtherEducationRate => "20%";

    // 4. Thâm niên
    public string UnderOneYearRate => "24%";
    public string OneToThreeYearsRate => "48%";
    public string OverThreeYearsRate => "28%";

    // 5. Chấm công chuyên cần
    public string AttendancePunctualRate => "97.4%";
    public string AttendanceLateEarlyRate => "2.6%";

    // 6. Quỹ lương tổng hợp
    public string TotalPayrollFund => "1.850.000.000 ₫";

    public HrStatisticsViewModel(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            await Task.Delay(200);
            LastUpdatedText = $"Cập nhật lúc {DateTime.Now:HH:mm}";
            IsRefreshing = false;
        });
    }

    protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
