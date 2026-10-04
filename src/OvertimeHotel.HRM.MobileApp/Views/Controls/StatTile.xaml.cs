using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Animations;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Thẻ chỉ số nhỏ (KPI) dùng chung cho cả hai nhánh: nhãn, số liệu lớn và dòng chú thích.
/// Thuần khiêm tốn — mọi nội dung do trang chủ truyền vào qua BindableProperty, nên mỗi nhánh
/// tự bố trí danh sách chỉ số riêng mà không phải nhân bản markup.
/// Khi <see cref="TappedCommand"/> được gán, thẻ tự trở thành nút bấm và đổi viền vàng kim.
/// </summary>
public partial class StatTile : ContentView
{
    /// <summary>Nhãn nhỏ phía trên, ví dụ "Ngày công".</summary>
    public static readonly BindableProperty CaptionProperty =
        BindableProperty.Create(nameof(Caption), typeof(string), typeof(StatTile), string.Empty, propertyChanged: OnCaptionChanged);

    /// <summary>Giá trị lớn ở giữa, ví dụ "22".</summary>
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(StatTile), string.Empty, propertyChanged: OnValueChanged);

    /// <summary>Dòng chú thích nhỏ phía dưới, ví dụ "công thực tế".</summary>
    public static readonly BindableProperty SubCaptionProperty =
        BindableProperty.Create(nameof(SubCaption), typeof(string), typeof(StatTile), string.Empty, propertyChanged: OnSubCaptionChanged);

    /// <summary>Màu của số liệu lớn — dùng để tô điểm nhấn cho chỉ số quan trọng nhất.</summary>
    public static readonly BindableProperty ValueColorProperty =
        BindableProperty.Create(nameof(ValueColor), typeof(Color), typeof(StatTile), Color.FromArgb("#1E3A8A"), propertyChanged: OnValueColorChanged);

    /// <summary>Lệnh chạy khi người dùng bấm vào thẻ — dùng cho thẻ điều hướng như "Đơn chờ duyệt".</summary>
    public static readonly BindableProperty TappedCommandProperty =
        BindableProperty.Create(nameof(TappedCommand), typeof(ICommand), typeof(StatTile), null, propertyChanged: OnTappedCommandChanged);

    public StatTile()
    {
        InitializeComponent();

        tileBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => _ = AnimateAndExecuteAsync())
        });
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string SubCaption
    {
        get => (string)GetValue(SubCaptionProperty);
        set => SetValue(SubCaptionProperty, value);
    }

    public Color ValueColor
    {
        get => (Color)GetValue(ValueColorProperty);
        set => SetValue(ValueColorProperty, value);
    }

    public ICommand? TappedCommand
    {
        get => (ICommand?)GetValue(TappedCommandProperty);
        set => SetValue(TappedCommandProperty, value);
    }

    /// <summary>Nhịp nảy nhẹ rồi mới chạy lệnh, để người dùng thấy phản hồi xúc giác.</summary>
    private async Task AnimateAndExecuteAsync()
    {
        if (TappedCommand is null)
        {
            return;
        }

        await tileBorder.SpringTapAsync();

        if (TappedCommand.CanExecute(null))
        {
            TappedCommand.Execute(null);
        }
    }

    private static void OnCaptionChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatTile)bindable).lblCaption.Text = newValue as string ?? string.Empty;

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatTile)bindable).lblValue.Text = newValue as string ?? string.Empty;

    private static void OnSubCaptionChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatTile)bindable).lblSubCaption.Text = newValue as string ?? string.Empty;

    private static void OnValueColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue is Color color)
        {
            ((StatTile)bindable).lblValue.TextColor = color;
        }
    }

    /// <summary>Có lệnh thì thẻ bấm được — đổi viền vàng kim để người dùng nhận ra ngay.</summary>
    private static void OnTappedCommandChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((StatTile)bindable).tileBorder.Stroke = newValue is null
            ? Color.FromArgb("#E2E8F0")
            : Color.FromArgb("#D4AF37");
}