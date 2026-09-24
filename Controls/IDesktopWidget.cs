namespace Chronvyr.Controls;

/// <summary>
/// 桌面控件窗口的统一契约，供 <see cref="Services.DesktopWidgetManager"/> 调度。
/// </summary>
public interface IDesktopWidget
{
    /// <summary>按设置中的 X / Y（屏幕物理像素）摆放控件。</summary>
    void ApplyLayout(double x, double y);

    /// <summary>关闭控件窗口。</summary>
    void CloseWidget();
}
