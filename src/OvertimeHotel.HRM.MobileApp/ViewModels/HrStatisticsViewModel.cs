using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.ViewModels;

public sealed class HrStatisticsViewModel : INotifyPropertyChanged
{
    private readonly HrSupabaseService _api;
    private bool _isRefreshing, _loading;
    private string _selectedPeriod = "Tháng hiện tại", _lastUpdatedText = "Chưa cập nhật", _error = "", _summary = "—", _attendance = "—", _payroll = "—";
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }
    public ObservableCollection<StatisticGroup> Departments { get; } = [];
    public ObservableCollection<StatisticGroup> Education { get; } = [];
    public ObservableCollection<StatisticGroup> Tenure { get; } = [];
    public string SelectedPeriod { get => _selectedPeriod; set => Set(ref _selectedPeriod, value); }
    public string LastUpdatedText { get => _lastUpdatedText; set => Set(ref _lastUpdatedText, value); }
    public string Error { get => _error; set { Set(ref _error, value); PropertyChanged?.Invoke(this, new(nameof(HasError))); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string Summary { get => _summary; set => Set(ref _summary, value); }
    public string Attendance { get => _attendance; set => Set(ref _attendance, value); }
    public string Payroll { get => _payroll; set => Set(ref _payroll, value); }
    public bool IsRefreshing { get => _isRefreshing; set => Set(ref _isRefreshing, value); }
    public HrStatisticsViewModel(HrSupabaseService api)
    {
        _api = api;
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        RefreshCommand = new Command(async () => await LoadAsync());
    }
    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true; IsRefreshing = true; Error = "";
        try
        {
            var data = await _api.GetStatisticsAsync();
            SelectedPeriod = data.Period;
            Summary = $"{data.TotalEmployees} nhân viên đang làm việc";
            Attendance = $"Đúng giờ: {data.PunctualRate:0.#}% · {data.AssessedShifts} ca được đánh giá\nTrễ / sớm: {data.ViolationCount} ca · Nghỉ được duyệt: {data.LeaveCount} ca · Thiếu quẹt: {data.MissingPunchCount} ca";
            Payroll = data.PayrollGross.HasValue
                ? $"Tổng lương đã tính (gộp): {data.PayrollGross:N0} đ\nĐã duyệt: {data.PayrollApproved:N0} đ · Nháp/chờ duyệt: {data.PayrollDraft:N0} đ\nSố đã tính không xác nhận tiền đã chuyển."
                : "Bạn không có quyền xem số liệu lương.";
            Replace(Departments, data.Departments); Replace(Education, data.Education); Replace(Tenure, data.Tenure);
            LastUpdatedText = $"Cập nhật lúc {DateTime.Now:HH:mm}";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UnauthorizedAccessException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Error = $"Không thể tải thống kê: {ex.Message}";
            Summary = Attendance = Payroll = "—"; Departments.Clear(); Education.Clear(); Tenure.Clear(); LastUpdatedText = "Chưa cập nhật";
        }
        finally { _loading = false; IsRefreshing = false; }
    }
    private static void Replace(ObservableCollection<StatisticGroup> target, IEnumerable<StatisticGroup>? source)
    { target.Clear(); foreach (var row in source ?? []) target.Add(row); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
}
public sealed class StatisticGroup
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public decimal Rate { get; set; }
    public string Display => $"{Name}: {Count} người ({Rate:0.#}%)";
}
public sealed class StatisticsResponse
{
    public string Period { get; set; } = "";
    public int TotalEmployees { get; set; }
    public List<StatisticGroup> Departments { get; set; } = [];
    public List<StatisticGroup> Education { get; set; } = [];
    public List<StatisticGroup> Tenure { get; set; } = [];
    public int AssessedShifts { get; set; }
    public decimal PunctualRate { get; set; }
    public int ViolationCount { get; set; }
    public int LeaveCount { get; set; }
    public int MissingPunchCount { get; set; }
    public decimal? PayrollGross { get; set; }
    public decimal? PayrollApproved { get; set; }
    public decimal? PayrollDraft { get; set; }
}
