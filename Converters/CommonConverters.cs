using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Chronvyr.Converters;

/// <summary>
/// 布尔值 → 透明度。用于把「已完成」的条目淡化显示。
/// </summary>
public sealed partial class BoolToOpacityConverter : IValueConverter
{
    /// <summary>值为 true 时的透明度。</summary>
    public double TrueOpacity { get; set; } = 0.45;

    /// <summary>值为 false 时的透明度。</summary>
    public double FalseOpacity { get; set; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? TrueOpacity : FalseOpacity;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// 布尔值取反后转换为 <see cref="Visibility"/>。用于「未完成时才显示」的元素。
/// </summary>
public sealed partial class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// 布尔值取反。
/// </summary>
public sealed partial class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is not true;
}

/// <summary>
/// 非空字符串 → <see cref="Visibility"/>。用于隐藏为空的描述文本。
/// </summary>
public sealed partial class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
