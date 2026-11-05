using System.ComponentModel;
using OvertimeHotel.HRM.MobileApp.Models;
using OvertimeHotel.HRM.MobileApp.ViewModels;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class PersonalAttendancePage : ContentPage
{
    private readonly AttendanceViewModel _attendance;
    public RequestsViewModel Requests { get; }

    public PersonalAttendancePage(AttendanceViewModel attendance, RequestsViewModel requests)
    {
        Requests = requests;
        InitializeComponent();
        BindingContext = _attendance = attendance;
        historyDate.Date = DateTime.Today;
        historyDate.MinimumDate = new DateTime(2000, 1, 1);
        historyDate.MaximumDate = DateTime.Today;
        historyDate.DateSelected += OnHistoryDateSelected;
        _attendance.PropertyChanged += OnAttendancePropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.WhenAll(_attendance.LoadDataAsync(), Requests.LoadAsync());
    }

    protected override void OnDisappearing()
    {
        Requests.CancelLoad();
        base.OnDisappearing();
    }

    private async void OnAttendancePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AttendanceViewModel.IsPopupVisible) && _attendance.IsPopupVisible)
        {
            await DisplayAlertAsync(_attendance.PopupTitle, _attendance.PopupMessage, _attendance.PopupButtonText);
            _attendance.ClosePopupCommand.Execute(null);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void OnFilterToggleClicked(object? sender, EventArgs e)
    {
        filterPanel.IsVisible = !filterPanel.IsVisible;
        filterToggle.Text = filterPanel.IsVisible ? "Bộ lọc ▴" : "Bộ lọc ▾";
    }

    private async void OnHistoryDateSelected(object? sender, DateChangedEventArgs e)
    {
        var date = Convert.ToDateTime(e.NewDate);
        historyDateLabel.Text = $"Đang lọc ngày {date:dd/MM/yyyy}";
        await _attendance.SelectHistoryDateAsync(date);
    }

    private void OnRequestsTabClicked(object? sender, EventArgs e) => SelectTab(showRequests: true);

    private void OnHistoryTabClicked(object? sender, EventArgs e) => SelectTab(showRequests: false);

    private void OnTodayTabClicked(object? sender, EventArgs e) => SelectHistoryTab(showToday: true);

    private void OnMonthTabClicked(object? sender, EventArgs e) => SelectHistoryTab(showToday: false);

    private void SelectHistoryTab(bool showToday)
    {
        todayPanel.IsVisible = showToday;
        monthPanel.IsVisible = !showToday;
        todayTab.BackgroundColor = Color.FromArgb(showToday ? "#1E3A8A" : "#EFF6FF");
        todayTab.TextColor = Color.FromArgb(showToday ? "#FFFFFF" : "#1E3A8A");
        monthTab.BackgroundColor = Color.FromArgb(showToday ? "#EFF6FF" : "#1E3A8A");
        monthTab.TextColor = Color.FromArgb(showToday ? "#1E3A8A" : "#FFFFFF");
    }

    private void SelectTab(bool showRequests)
    {
        requestsPanel.IsVisible = showRequests;
        historyPanel.IsVisible = !showRequests;
        requestsTab.BackgroundColor = Color.FromArgb(showRequests ? "#1E3A8A" : "#EFF6FF");
        requestsTab.TextColor = Color.FromArgb(showRequests ? "#FFFFFF" : "#1E3A8A");
        historyTab.BackgroundColor = Color.FromArgb(showRequests ? "#EFF6FF" : "#1E3A8A");
        historyTab.TextColor = Color.FromArgb(showRequests ? "#1E3A8A" : "#FFFFFF");
    }

    private async void OnRecordTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject { BindingContext: AttendanceRecord record }) return;
        await DisplayAlertAsync($"{record.NgayLamViec} · {record.TenCa}",
            $"{record.TrangThaiText}\nVào: {record.CheckInText}  ·  Ra: {record.CheckOutText}\nĐi trễ: {record.PhutDiTreText}  ·  Về sớm: {record.PhutVeSomText}\nLàm thêm: {record.GioOtText}", "Đóng");
    }
}
