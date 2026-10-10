using System.Runtime.Versioning;

namespace OvertimeHotel.HRM.MobileApp.Animations;

/// <summary>
/// Chừa khoảng đệm của hệ thống cho nội dung trang, tránh đè lên thanh trạng thái.
///
/// ⚠️ KHÔNG còn trang nào trong ứng dụng gọi lớp này — giữ lại làm công cụ dự phòng.
/// Đo trên AVD API 35 (720×1640 px, mật độ 2.0) cho thấy Shell ĐÃ tự chừa sẵn
/// khoảng cho thanh trạng thái: trang gốc của tab bắt đầu ở ~37.5 dp, trong khi
/// vùng an toàn thật chỉ ~33 dp. Gọi <see cref="Track"/> trên những trang đó đẩy
/// nội dung xuống ~69 dp, sinh đúng khoảng trống dài ngay trên đầu header.
///
/// Trang mẫu xem đúng là <c>Views/LeaveApprovalPage</c> (trang duyệt đơn) — không
/// gọi <see cref="Track"/> và hiển thị sát đầu trang. Muốn bỏ khoảng trống thì cứ
/// khai Padding trực tiếp trong XAML như các trang đó.
///
/// Cách dùng nếu sau này cần: gọi <see cref="Track"/> một lần trong hàm khởi tạo
/// trang. Trang tự điều chỉnh lại mỗi khi vừa hiện và mỗi khi đổi kích thước (xoay
/// máy, bật/tắt thanh điều hướng) — không phải chỉ tính một lần lúc khởi tạo.
/// </summary>
public static class SafeAreaInset
{
	/// <summary>
	/// Gắn trang <paramref name="page"/> với phần tử nội dung <paramref name="body"/>
	/// để tự cộng khoảng đệm hệ thống vào Padding của <paramref name="body"/>.
	/// </summary>
	/// <param name="page">Trang chứa nội dung; dùng làm mốc cho sự kiện vòng đời.</param>
	/// <param name="body">Phần tử cuốn (thường là VerticalStackLayout) cần chừa inset.</param>
	/// <param name="basePadding">Padding gốc khai trong XAML, dùng làm gốc để cộng lại.</param>
	public static void Track(ContentPage page, Layout body, Thickness basePadding)
	{
		ArgumentNullException.ThrowIfNull(page);
		ArgumentNullException.ThrowIfNull(body);

		// Apply cộng vào basePadding cố định chứ không cộng vào Padding hiện tại,
		// nên gọp nhiều lần vẫn cho ra cùng một kết quả — an toàn khi sự kiện bắn lặp.
		page.Loaded += (_, _) => Apply(body, basePadding);
		page.SizeChanged += (_, _) => Apply(body, basePadding);
	}

	/// <summary>
	/// Cộng khoảng đệm hệ thống vào Padding của <paramref name="target"/>, giữ nguyên
	/// phần Padding gốc do XAML khai báo.
	/// </summary>
	public static void Apply(Layout target, Thickness basePadding)
	{
		ArgumentNullException.ThrowIfNull(target);

		Thickness system = GetSystemInset(target);

		target.Padding = new Thickness(
			basePadding.Left + system.Left,
			basePadding.Top + system.Top,
			basePadding.Right + system.Right,
			basePadding.Bottom);
	}

	/// <summary>
	/// Đọc khoảng đệm hệ thống của nền tảng hiện tại. Nền tảng khác Android trả về
	/// Thickness.Zero vì MAUI đã tự lo phần này (ví dụ iOS đã áp UseSafeArea).
	/// </summary>
	private static Thickness GetSystemInset(Layout target)
	{
		if (DeviceInfo.Current.Platform == DevicePlatform.Android)
		{
#if ANDROID
			return GetAndroidInset(target);
#else
			// Các hàm bên dưới khai báo tham số kiểu Android.* nên chỉ tồn tại khi
			// build cho Android — iOS/MacCatalyst/Windows không tham chiếu
			// Mono.Android.dll và sẽ không bind được chúng. Ở các nền tảng đó nhánh
			// trên không bao giờ chạy tới (DeviceInfo trả về nền tảng khác), nên
			// Thickness.Zero vẫn là kết quả đúng.
			return Thickness.Zero;
#endif
		}

		return Thickness.Zero;
	}

#if ANDROID
	// WindowInsets.Type.SystemBars/GetInsets chỉ có từ Android 11 (API 30). Trên máy
	// cũ hơn, hệ thống chưa vẽ tràn viền nên nội dung vốn đã nằm trong vùng an toàn —
	// trả Zero là đúng. Nhánh kiểm tra phiên bản phải nằm ngay tại chỗ gọi để trợ
	// giảm phân tích nền tảng chứng minh được lệnh gọi hợp lệ (không sinh CA1416).
	private static Thickness GetAndroidInset(Layout target)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			return Thickness.Zero;
		}

		Android.Views.View? native = target.Handler?.PlatformView as Android.Views.View;

		if (native is null)
		{
			return Thickness.Zero;
		}

		return ReadInsetPixels(native);
	}

	[SupportedOSPlatform("android30.0")]
	private static Thickness ReadInsetPixels(Android.Views.View view)
	{
		Android.Graphics.Insets bars =
			view.RootWindowInsets?.GetInsets(Android.Views.WindowInsets.Type.SystemBars())
			?? Android.Graphics.Insets.None;

		// Inset trả về theo pixel thực, còn Padding của MAUI tính theo đơn vị thiết
		// kế (device-independent) — phải chia cho mật độ màn hình.
		double density = view.Context?.Resources?.DisplayMetrics?.Density ?? 1.0;

		return new Thickness(
			bars.Left / density,
			bars.Top / density,
			bars.Right / density,
			bars.Bottom / density);
	}
#endif
}