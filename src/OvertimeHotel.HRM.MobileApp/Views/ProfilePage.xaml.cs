using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.ViewModels;
using OvertimeHotel.HRM.MobileApp.Services;
using System.Text.Json;

namespace OvertimeHotel.HRM.MobileApp.Views;

/// <summary>
/// Màn Thông tin cá nhân. Mở từ dòng menu của tab Cài Đặt, hoặc từ thẻ
/// "Hồ Sơ" trên trang chủ — cả hai đều là trang push nên có nút quay lại.
/// Phần đổi mật khẩu đã tách sang <see cref="ChangePasswordPage"/>.
/// </summary>
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private readonly SupabaseRestClient _rest;
    private readonly IAuthService _auth;
    private bool _busy;

    public ProfilePage(ProfileViewModel viewModel, SupabaseRestClient rest, IAuthService auth)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        _rest = rest;
        _auth = auth;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUserInfo();

        // 1. Đặt vị trí ban đầu (Opacity đã là 0 trong XAML nên hoàn toàn không bị chớp hay giật)
        profileHeader.TranslationY = -25;
        identityCard.TranslationY = 35;

        await Task.Delay(50);

        // 2. Chạy chuỗi hoạt ảnh trượt vào êm ái bằng CubicOut (mượt mà 60 FPS, không khựng)
        _ = Task.WhenAll(
            profileHeader.FadeToAsync(1.0, 400, Easing.CubicOut),
            profileHeader.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        await Task.Delay(110);
        _ = Task.WhenAll(
            identityCard.FadeToAsync(1.0, 450, Easing.CubicOut),
            identityCard.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );
    }

    private async void OnBackButtonClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        await Shell.Current.GoToAsync("..");
    }
    private sealed record Contact(string? Phone, string? PersonalEmail, string? Address);

    private void SetContactEnabled(bool enabled)
    {
        phoneEntry.IsEnabled = personalEmailEntry.IsEnabled = addressEntry.IsEnabled = saveContactButton.IsEnabled = enabled;
    }

    private async Task LoadContactAsync()
    {
        SetContactEnabled(false);
        reloadContactButton.IsVisible = false;
        if (_auth.IsDemoMode) { contactStatus.Text = "Chỉnh sửa hồ sơ không khả dụng trong chế độ demo."; return; }
        try
        {
            var json = await _rest.PostAsync("rpc/hrm_my_contact", JsonSerializer.Serialize(new { p_action = "get" }));
            var contact = JsonSerializer.Deserialize<Contact>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Không đọc được thông tin liên hệ.");
            phoneEntry.Text = contact.Phone ?? "";
            personalEmailEntry.Text = contact.PersonalEmail ?? "";
            addressEntry.Text = contact.Address ?? "";
            contactStatus.Text = "";
            SetContactEnabled(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UnauthorizedAccessException or JsonException or TaskCanceledException)
        {
            contactStatus.Text = "Không tải được thông tin liên hệ. Vui lòng thử lại.";
            reloadContactButton.IsVisible = true;
        }
    }

    private async void OnEditContactClicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        profileStatus.Text = "";
        contactOverlay.IsVisible = true;
        await LoadContactAsync();
    }

    private void OnCancelContactClicked(object? sender, EventArgs e)
    {
        if (!_busy) contactOverlay.IsVisible = false;
    }

    protected override bool OnBackButtonPressed()
    {
        if (contactOverlay.IsVisible) { if (!_busy) contactOverlay.IsVisible = false; return true; }
        return base.OnBackButtonPressed();
    }

    private async void OnReloadContactClicked(object? sender, EventArgs e) => await LoadContactAsync();

    private async void OnSaveContactClicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        var phone = (phoneEntry.Text ?? "").Trim();
        var email = (personalEmailEntry.Text ?? "").Trim();
        var address = (addressEntry.Text ?? "").Trim();
        var error = ProfileContactValidation.Validate(phone, email, address);
        if (error != null) { contactStatus.Text = error; return; }
        _busy = true;
        cancelContactButton.IsEnabled = false;
        SetContactEnabled(false);
        contactStatus.Text = "Đang lưu...";
        try
        {
            var json = await _rest.PostAsync("rpc/hrm_my_contact", JsonSerializer.Serialize(new { p_action = "update", p_phone = phone, p_email = email, p_address = address }));
            var contact = JsonSerializer.Deserialize<Contact>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Không nhận được kết quả lưu.");
            phoneEntry.Text = contact.Phone ?? ""; personalEmailEntry.Text = contact.PersonalEmail ?? ""; addressEntry.Text = contact.Address ?? "";
            if (_auth.CurrentUser is { } user) user.DienThoai = contact.Phone ?? "";
            _viewModel.LoadUserInfo();
            contactOverlay.IsVisible = false;
            profileStatus.Text = "Đã lưu thông tin liên hệ.";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UnauthorizedAccessException or JsonException or TaskCanceledException)
        {
            contactStatus.Text = "Không thể xác nhận đã lưu. Hãy tải lại thông tin và thử lại.";
            reloadContactButton.IsVisible = true;
            return;
        }
        finally { _busy = false; cancelContactButton.IsEnabled = true; }
        SetContactEnabled(true);
    }
}
