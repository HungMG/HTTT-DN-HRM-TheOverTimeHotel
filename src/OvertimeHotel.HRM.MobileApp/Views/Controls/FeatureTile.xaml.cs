using System.Windows.Input;
using OvertimeHotel.HRM.MobileApp.Animations;

namespace OvertimeHotel.HRM.MobileApp.Views.Controls;

/// <summary>
/// Thẻ chức năng nghiệp vụ dùng chung cho cả hai nhánh: biểu tượng, tên tính năng,
/// mô tả ngắn và nút hành động. Mỗi nhánh tự chọn bộ tính năng riêng nên danh sách
/// lưới thẻ trên màn hình khác nhau rõ rệt.
/// </summary>
public partial class FeatureTile : ContentView
{
    /// <summary>Ký tự biểu tượng hiển thị ở góc trên thẻ.</summary>
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(FeatureTile), "★", propertyChanged: OnIconChanged);

    /// <summary>Tên tính năng, ví dụ "Chấm Công".</summary>
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FeatureTile), string.Empty, propertyChanged: OnTitleChanged);

    /// <summary>Mô tả ngắn một dòng, ví dụ "Quẹt thẻ &amp; giờ OT".</summary>
    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(FeatureTile), string.Empty, propertyChanged: OnDescriptionChanged);

    /// <summary>Nhãn trên nút hành động, ví dụ "Chi Tiết".</summary>
    public static readonly BindableProperty ActionTextProperty =
        BindableProperty.Create(nameof(ActionText), typeof(string), typeof(FeatureTile), "Mở", propertyChanged: OnActionTextChanged);

    /// <summary>Màu nền nút hành động — cho phép nổi bật nút chính của nhánh quản lý.</summary>
    public static readonly BindableProperty ActionColorProperty =
        BindableProperty.Create(nameof(ActionColor), typeof(Color), typeof(FeatureTile), Color.FromArgb("#1E3A8A"), propertyChanged: OnActionColorChanged);

    /// <summary>Lệnh chạy khi người dùng bấm nút hành động.</summary>
    public static readonly BindableProperty TappedCommandProperty =
        BindableProperty.Create(nameof(TappedCommand), typeof(ICommand), typeof(FeatureTile), null);

    /// <summary>Tham số truyền cho <see cref="TappedCommand"/> — thường là tên tính năng.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(FeatureTile), null);

    public FeatureTile()
    {
        InitializeComponent();
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public Color ActionColor
    {
        get => (Color)GetValue(ActionColorProperty);
        set => SetValue(ActionColorProperty, value);
    }

    public ICommand? TappedCommand
    {
        get => (ICommand?)GetValue(TappedCommandProperty);
        set => SetValue(TappedCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnIconChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((FeatureTile)bindable).lblIcon.Text = newValue as string ?? "★";

    private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((FeatureTile)bindable).lblTitle.Text = newValue as string ?? string.Empty;

    private static void OnDescriptionChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((FeatureTile)bindable).lblDescription.Text = newValue as string ?? string.Empty;

    private static void OnActionTextChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((FeatureTile)bindable).btnAction.Text = newValue as string ?? "Mở";

    private static void OnActionColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (newValue is Color color)
        {
            ((FeatureTile)bindable).btnAction.BackgroundColor = color;
        }
    }

    private async void OnActionClicked(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            await element.SpringTapAsync();
        }

        if (TappedCommand?.CanExecute(CommandParameter) == true)
        {
            TappedCommand.Execute(CommandParameter);
        }
    }
}