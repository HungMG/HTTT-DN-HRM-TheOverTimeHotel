using System.Diagnostics;
using OvertimeHotel.HRM.MobileApp.Services;

namespace OvertimeHotel.HRM.MobileApp.Views;

public partial class IntroPage : ContentPage
{
    private const double MotoMaxTravel = 260.0;
    private const double TotalAnimationDurationMs = 2200.0;

    private readonly IAuthService _authService;

    public IntroPage(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Đặt trạng thái ban đầu bằng GPU Transform
        progressFill.ScaleX = 0;
        motoRiderGroup.TranslationX = 0;
        motoIcon.TranslationY = 0;
        lblPercent.Text = "0%";
        introRoot.Opacity = 1;

        await Task.Delay(150);

        // 2. Chạy tác vụ kiểm tra khôi phục phiên ngầm trên Background Thread (hoàn toàn không chặn UI Thread)
        var checkSessionTask = Task.Run(() => _authService.TryAutoLoginAsync());

        // 3. Chạy vòng lặp hoạt ảnh dựa trên thời gian thực Stopwatch (loại bỏ 100% hiện tượng khựng/lag)
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < TotalAnimationDurationMs)
        {
            var rawT = Math.Clamp(stopwatch.ElapsedMilliseconds / TotalAnimationDurationMs, 0.0, 1.0);

            // Đường cong chuyển động gia tốc mềm mại chuẩn 60 FPS
            var eased = rawT < 0.5 
                ? 4 * rawT * rawT * rawT 
                : 1 - Math.Pow(-2 * rawT + 2, 3) / 2;

            // Cập nhật thuộc tính Transform (GPU Hardware Accelerated - 0ms layout pass)
            progressFill.ScaleX = eased;
            
            // Cụm xe mô tô và số phần trăm ở dưới chạy tịnh tiến cùng nhau
            motoRiderGroup.TranslationX = eased * MotoMaxTravel;
            
            // Độ nhún nhẹ của riêng chiếc xe khi chạy
            motoIcon.TranslationY = Math.Sin(rawT * 28) * 2.0;

            var percentVal = (int)Math.Min(100, Math.Round(eased * 100));
            lblPercent.Text = $"{percentVal}%";

            await Task.Delay(16); // Đồng bộ tần số quét ~60 FPS
        }

        // Đạt 100% chính xác
        progressFill.ScaleX = 1.0;
        motoRiderGroup.TranslationX = MotoMaxTravel;
        motoIcon.TranslationY = 0;
        lblPercent.Text = "100%";

        var isSessionRestored = await checkSessionTask;

        await Task.Delay(150);

        // Cụm xe mô tô cùng huy hiệu 100% tăng tốc phóng vút về phía trước thoát màn hình
        _ = motoRiderGroup.TranslateToAsync(420, 0, 320, Easing.CubicIn);

        // Toàn bộ màn hình mờ dần (Fade Out) êm ái
        await introRoot.FadeToAsync(0, 380, Easing.CubicOut);

        // 4. ĐIỀU HƯỚNG THEO TRẠNG THÁI PHIÊN ĐĂNG NHẬP
        if (Shell.Current != null)
        {
            if (isSessionRestored && _authService.IsAuthenticated)
            {
                // Đã có phiên đăng nhập: Vào thẳng Trang Chủ
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                // Chưa đăng nhập: Vào trang Đăng Nhập
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
    }
}
