using Chronvyr.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace Chronvyr.Helpers;

/// <summary>
/// 承载托盘菜单的隐身窗口。
/// </summary>
/// <remarks>
/// WinUI 的 <see cref="MenuFlyout"/> 必须锚在某个 UIElement 上、且需要一个 XamlRoot，
/// 而托盘图标根本没有窗口，所以借一个 1×1 的置顶窗口来弹出菜单。
/// <para>
/// 为什么要费这个劲：<b>Win32 原生菜单永远拿不到亚克力/云母</b>（没有任何官方 API），
/// 而 WinUI 的 MenuFlyoutPresenter 自带 Desktop Acrylic。所以「现代外观」不是画出来的，
/// 是用对控件之后系统白送的——前提是设上 <c>ShouldConstrainToRootBounds = false</c>，
/// 否则背景材质根本不会渲染。
/// </para>
/// </remarks>
public sealed class TrayFlyoutWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExLayered = 0x00080000L;
    private const uint LwaAlpha = 0x00000002;

    private static readonly nint HwndTopmost = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly Grid _anchor;
    private readonly MenuFlyout _menu;

    public TrayFlyoutWindow(Action onOpen, Action onSettings, Action onExit)
    {
        // anchor 必须钉在窗口左上角且真的只有 1×1：
        // Window 的内容默认会被拉伸填满窗口，而 WinUI 3 的窗口有最小尺寸（约 136×38）。
        // 锚点一旦被撑到那么大，菜单就会围绕那个矩形定位——表现就是
        // 「菜单和托盘图标在横向上差了 130 多像素」，约等于一个窗口宽度。
        _anchor = new Grid
        {
            Width = 1,
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        Content = _anchor;

        _menu = new MenuFlyout
        {
            // 这一行是硬性前提：不设它，SystemBackdrop 不会被渲染，
            // 菜单就会是没有材质的素面板。
            ShouldConstrainToRootBounds = false,

            // 菜单按微软规范用 Acrylic（Mica 是给主窗口背景的）。
            SystemBackdrop = new DesktopAcrylicBackdrop(),

            // 托盘在屏幕底部，菜单必须向上展开，否则会被任务栏挡住。
            Placement = FlyoutPlacementMode.Top,
        };

        _menu.Items.Add(MakeItem("TrayOpen", onOpen));
        _menu.Items.Add(MakeItem("TraySettings", onSettings));
        _menu.Items.Add(new MenuFlyoutSeparator());
        _menu.Items.Add(MakeItem("TrayExit", onExit));

        // 菜单关掉就把承载窗口藏起来，不然会留一个 1×1 的窗口在屏幕上。
        _menu.Closed += (_, _) => AppWindow.Hide();

        ConfigureInvisibleWindow();
    }

    /// <summary>在鼠标位置弹出菜单。</summary>
    public void ShowAtCursor()
    {
        if (!GetCursorPos(out var point))
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(this);

        // 用 SetWindowPos 同步搬运，而不是 AppWindow.Move：后者不保证在本次调用内
        // 生效，菜单可能从窗口的旧位置弹出来——表现就是「菜单和托盘图标对不上」。
        _ = SetWindowPos(hwnd, HwndTopmost, point.X, point.Y, 0, 0,
            SwpNoSize | SwpNoActivate | SwpShowWindow);

        Activate();

        // 让菜单能正确 light-dismiss：不把自己设成前台窗口的话，
        // 点击菜单外面它不会消失。
        SetForegroundWindow(hwnd);

        _menu.ShowAt(_anchor, new Windows.Foundation.Point(0, 0));
    }

    private static MenuFlyoutItem MakeItem(string locKey, Action action)
    {
        var item = new MenuFlyoutItem { Text = App.Loc[locKey] };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>把窗口做成「看不见的菜单锚点」：1×1、透明、不进任务栏与 Alt+Tab。</summary>
    private void ConfigureInvisibleWindow()
    {
        AppWindow.Resize(new SizeInt32(1, 1));
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            // 承载窗口必须置顶：它自己是普通窗口的话，弹出的菜单会被更靠前的主界面
            // 盖住下半截——右键菜单看起来「缺了一块」就是这么来的。
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(hwnd, GwlExStyle, (nint)(style | WsExToolWindow | WsExNoActivate | WsExLayered));

        // 整个承载窗口设成全透明。WinUI 3 的窗口有最小尺寸限制（约 136×38），
        // 单靠 Resize(1,1) 会留下一块看得见的空白。菜单本身是独立的 popup 窗口，
        // 不受这里影响，所以把锚点这一层彻底藏掉是安全的。
        _ = SetLayeredWindowAttributes(hwnd, 0, 0, LwaAlpha);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>把整个窗口设成全透明（bAlpha = 0）。</summary>
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(nint hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
