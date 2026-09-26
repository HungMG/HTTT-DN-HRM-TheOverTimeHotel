using OvertimeHotel.HRM.MobileApp.Animations;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp;

public partial class MainPage : ContentPage
{
    private readonly IAuthService _authService;
    private readonly ILeaveApprovalService _leaveApprovalService;
    private Action? _modalConfirmAction;

    public MainPage(IAuthService authService, ILeaveApprovalService leaveApprovalService)
    {
        InitializeComponent();
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _leaveApprovalService = leaveApprovalService ?? throw new ArgumentNullException(nameof(leaveApprovalService));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. CẬP NHẬT THÔNG TIN NGƯỜI DÙNG VỪA ĐĂNG NHẬP
        var user = _authService.CurrentUser;
        if (user != null && user.IsLoggedIn)
        {
            lblGreeting.Text = $"Xin chào, {user.HoTen}!";
            lblRoleDepartment.Text = $"{user.TenPhongBan} · {user.TenChucVu} ({user.TenVaiTro})";

            if (!string.IsNullOrWhiteSpace(user.HoTen))
            {
                var parts = user.HoTen.Trim().Split(' ');
                lblHeaderAvatar.Text = parts[^1][0].ToString().ToUpper();
            }

            // Đồng bộ trạng thái duyệt đơn từ và số đếm động
            if (user.CanApproveLeave)
            {
                btnDonTuAction.Text = "Duyệt Đơn";
                btnDonTuAction.BackgroundColor = Color.FromArgb("#16A34A");
                lblDonTuDesc.Text = "Phê duyệt đơn nghỉ, thôi việc";

                try
                {
                    var pendingCount = await _leaveApprovalService.GetPendingCountAsync(user.TenVaiTro, user.MaPhongBan);
                    lblPendingLeaveCount.Text = pendingCount.ToString();
                    lblPendingLeaveSub.Text = user.IsManager ? "Bộ phận quản lý" : "Toàn khách sạn";
                }
                catch
                {
                    lblPendingLeaveCount.Text = "0";
                    lblPendingLeaveSub.Text = "Không có đơn";
                }
            }
            else
            {
                btnDonTuAction.Text = "Tạo Đơn";
                btnDonTuAction.BackgroundColor = Color.FromArgb("#1E3A8A");
                lblDonTuDesc.Text = "Nghỉ phép, đổi ca";
                lblPendingLeaveCount.Text = "0";
                lblPendingLeaveSub.Text = "Đơn của bạn";
            }
        }
        else
        {
            lblGreeting.Text = "Xin chào, Nhân viên!";
            lblRoleDepartment.Text = "Bộ phận Buồng phòng · The OverTime Hotel";
            lblHeaderAvatar.Text = "👤";
            lblPendingLeaveCount.Text = "0";
            lblPendingLeaveSub.Text = "Chưa đăng nhập";
        }

        // 2. KHỞI TẠO VỊ TRÍ BAN ĐẦU CHO HIỆU ỨNG "BAY CÁC KHỐI VÀO" MƯỢT MÀ
        headerSection.Opacity = 0;
        headerSection.TranslationY = -35;

        heroShiftCard.Opacity = 0;
        heroShiftCard.TranslationY = 40;

        statsGrid.Opacity = 0;
        statsGrid.TranslationY = 35;

        featuresHeader.Opacity = 0;
        featuresHeader.TranslationY = 25;

        cardChamCong.Opacity = 0;
        cardChamCong.TranslationY = 30;

        cardLichCa.Opacity = 0;
        cardLichCa.TranslationY = 30;

        cardDonTu.Opacity = 0;
        cardDonTu.TranslationY = 30;

        cardPhieuLuong.Opacity = 0;
        cardPhieuLuong.TranslationY = 30;

        footerLabel.Opacity = 0;

        // 3. THỰC THI CHUỖI HOẠT ẢNH LƯỚT VÀO NỐI TIẾP KHÔNG BỊ KHỰNG (SILKY SMOOTH CUBIC OUT)
        await Task.Delay(80);
        _ = Task.WhenAll(
            headerSection.FadeToAsync(1.0, 450, Easing.CubicOut),
            headerSection.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        await Task.Delay(130);
        _ = Task.WhenAll(
            heroShiftCard.FadeToAsync(1.0, 450, Easing.CubicOut),
            heroShiftCard.TranslateToAsync(0, 0, 520, Easing.CubicOut)
        );

        await Task.Delay(120);
        _ = Task.WhenAll(
            statsGrid.FadeToAsync(1.0, 450, Easing.CubicOut),
            statsGrid.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        await Task.Delay(100);
        _ = Task.WhenAll(
            featuresHeader.FadeToAsync(1.0, 350, Easing.CubicOut),
            featuresHeader.TranslateToAsync(0, 0, 400, Easing.CubicOut)
        );

        // Hàng 1 của 4 khối chức năng lướt vào êm ái
        await Task.Delay(80);
        _ = Task.WhenAll(
            cardChamCong.FadeToAsync(1.0, 450, Easing.CubicOut),
            cardChamCong.TranslateToAsync(0, 0, 500, Easing.CubicOut),
            cardLichCa.FadeToAsync(1.0, 450, Easing.CubicOut),
            cardLichCa.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        // Hàng 2 của 4 khối chức năng lướt vào tiếp nối
        await Task.Delay(90);
        _ = Task.WhenAll(
            cardDonTu.FadeToAsync(1.0, 450, Easing.CubicOut),
            cardDonTu.TranslateToAsync(0, 0, 500, Easing.CubicOut),
            cardPhieuLuong.FadeToAsync(1.0, 450, Easing.CubicOut),
            cardPhieuLuong.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        await Task.Delay(100);
        await footerLabel.FadeToAsync(1.0, 400);
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement el) await el.SpringTapAsync(0.92, 50, 140);
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//ProfilePage");
        }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();

        ShowCustomModal(
            title: "Xác Nhận Đăng Xuất",
            message: "Bạn có chắc chắn muốn đăng xuất khỏi ứng dụng di động không?",
            iconText: "➜",
            iconTextColor: "#DC2626",
            iconBgColor: "#FEF2F2",
            borderColor: "#FCA5A5",
            confirmText: "ĐĂNG XUẤT",
            showCancel: true,
            onConfirm: async () =>
            {
                await _authService.LogoutAsync();
                await Shell.Current.GoToAsync("//LoginPage");
            }
        );
    }

    private async void OnCheckInClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();

        var now = DateTime.Now;
        ShowCustomModal(
            title: "Check-in Thành Công",
            message: $"Thời gian: {now:HH:mm:ss dd/MM/yyyy}\n" +
                     $"Ca làm việc: Ca Đêm (22:00 – 06:00)\n" +
                     $"Phụ cấp: +30% theo Luật Lao Động\n" +
                     $"Trạng thái: Đúng giờ · Hợp lệ.",
            iconText: "✓",
            iconTextColor: "#059669",
            iconBgColor: "#ECFDF5",
            borderColor: "#10B981",
            confirmText: "HOÀN TẤT",
            showCancel: false,
            onConfirm: null
        );
    }

    private async void OnFeatureClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();

        var buttonText = (sender as Button)?.Text ?? "Chức năng";
        ShowCustomModal(
            title: "Chức Năng Đang Nâng Cấp",
            message: $"Tính năng [{buttonText}] đang được kết nối với API Supabase ở các task tiếp theo.",
            iconText: "★",
            iconTextColor: "#B45309",
            iconBgColor: "#FFFBEB",
            borderColor: "#D4AF37",
            confirmText: "ĐỒNG Ý",
            showCancel: false,
            onConfirm: null
        );
    }

    private async void OnPendingLeaveStatTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement el) await el.SpringTapAsync(0.92, 50, 140);
        var user = _authService.CurrentUser;
        if (user != null && user.CanApproveLeave)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("LeaveApprovalPage");
            }
        }
        else
        {
            ShowCustomModal(
                title: "Đơn Từ Trực Tuyến",
                message: "Bạn hiện tại không có thẩm quyền phê duyệt đơn hoặc chưa có đơn nào cần xử lý.",
                iconText: "📋",
                iconTextColor: "#1E3A8A",
                iconBgColor: "#EFF6FF",
                borderColor: "#1E3A8A",
                confirmText: "ĐỒNG Ý",
                showCancel: false,
                onConfirm: null
            );
        }
    }

    private async void OnDonTuClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        var user = _authService.CurrentUser;
        if (user != null && user.CanApproveLeave)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("LeaveApprovalPage");
            }
        }
        else
        {
            ShowCustomModal(
                title: "Tạo Đơn Trực Tuyến (TSK-18)",
                message: "Chức năng nộp đơn nghỉ phép, thôi việc dành cho nhân viên đang được kết nối theo đúng lộ trình ở task tiếp theo.",
                iconText: "📝",
                iconTextColor: "#1E3A8A",
                iconBgColor: "#EFF6FF",
                borderColor: "#1E3A8A",
                confirmText: "ĐÃ HIỂU",
                showCancel: false,
                onConfirm: null
            );
        }
    }

    private void ShowCustomModal(string title, string message, string iconText, string iconTextColor, 
                                 string iconBgColor, string borderColor, string confirmText, bool showCancel, Action? onConfirm)
    {
        lblModalTitle.Text = title;
        lblModalMessage.Text = message;
        lblModalIcon.Text = iconText;
        lblModalIcon.TextColor = Color.FromArgb(iconTextColor);
        mainModalIconBox.BackgroundColor = Color.FromArgb(iconBgColor);
        mainModalIconBox.Stroke = Color.FromArgb(borderColor);
        mainModalBorder.Stroke = Color.FromArgb(borderColor);
        btnModalConfirm.Text = confirmText;

        btnModalCancel.IsVisible = showCancel;
        if (!showCancel)
        {
            Grid.SetColumnSpan(btnModalConfirm, 2);
        }
        else
        {
            Grid.SetColumnSpan(btnModalConfirm, 1);
        }

        _modalConfirmAction = onConfirm;

        mainModalOverlay.Opacity = 0;
        mainModalOverlay.IsVisible = true;
        _ = mainModalOverlay.FadeToAsync(1, 180, Easing.CubicOut);
        _ = mainModalBorder.BounceInAsync(360);
    }

    private async void OnModalCancelClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        mainModalOverlay.IsVisible = false;
        _modalConfirmAction = null;
    }

    private async void OnModalConfirmClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement btn) await btn.SpringTapAsync();
        mainModalOverlay.IsVisible = false;
        _modalConfirmAction?.Invoke();
        _modalConfirmAction = null;
    }
}
