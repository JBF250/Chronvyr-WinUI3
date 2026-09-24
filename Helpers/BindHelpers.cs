namespace Chronvyr.Helpers;

/// <summary>
/// 供 x:Bind 函数绑定使用的静态辅助方法。
/// 桌面控件窗口（<c>Window</c>）不是 <c>FrameworkElement</c>，其 DataTemplate 中
/// 无法使用 <c>Converter={StaticResource ...}</c>（需要转换器查找根），
/// 因此这类场景统一改用静态函数绑定。
/// </summary>
public static class BindHelpers
{
    /// <summary>已完成条目淡化显示。</summary>
    public static double CompletedOpacity(bool isCompleted) => isCompleted ? 0.45 : 1.0;
}
