using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Chronvyr.Helpers;

/// <summary>
/// 窗口相关的 Win32 / AppWindow 辅助操作。
/// </summary>
public static class WindowHelper
{
    private const int SwHide = 0;
    private const int SwShow = 5;

    /// <summary>获取窗口句柄。</summary>
    public static nint GetHandle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    /// <summary>应用图标文件路径（随程序一起发布）。</summary>
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Chronvyr.ico");

    /// <summary>
    /// 给窗口设置标题栏图标。WinUI 3 默认不会自动带上 exe 的图标，
    /// 所有窗口都必须显式调用，否则标题栏左上角是空白。
    /// </summary>
    public static void ApplyAppIcon(Window window)
    {
        try
        {
            var path = IconPath;
            if (File.Exists(path))
            {
                window.AppWindow.SetIcon(path);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Services.Log.WriteException("设置窗口图标失败", ex);
        }
    }

    /// <summary>隐藏窗口（主窗口关闭时使用，FR-8.4）。</summary>
    public static void Hide(Window window) => ShowWindow(GetHandle(window), SwHide);

    /// <summary>显示并激活窗口。</summary>
    public static void Show(Window window)
    {
        var hwnd = GetHandle(window);
        ShowWindow(hwnd, SwShow);
        SetForegroundWindow(hwnd);
        window.Activate();
    }

    /// <summary>
    /// 按 FR-1.1 计算并应用主窗口尺寸：读取屏幕分辨率后「降低一档」，并居中显示。
    /// 当前实现为显示器工作区的 5/6 等比缩放（1920×1080 → 1600×900）。
    /// </summary>
    public static void ApplyDefaultSize(Window window)
    {
        var hwnd = GetHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        if (area is null)
        {
            return;
        }

        var work = area.WorkArea;

        // FR-1.1：降低一档（5/6 等比）。低于最小可用尺寸时不低于 MinWidth/MinHeight。
        var width = (int)Math.Round(work.Width * 5d / 6d);
        var height = (int)Math.Round(work.Height * 5d / 6d);
        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);

        // 居中对齐显示器工作区。
        var x = work.X + ((work.Width - width) / 2);
        var y = work.Y + ((work.Height - height) / 2);

        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>主窗口最小宽度。</summary>
    public const int MinWidth = 960;

    /// <summary>主窗口最小高度。</summary>
    public const int MinHeight = 640;

    /// <summary>
    /// 激活已存在的同名窗口（用于单实例：第二次启动时把已有主窗口拉到前台）。
    /// </summary>
    public static bool TryActivateExistingWindow(string title)
    {
        var hwnd = FindWindow(null, title);
        if (hwnd == 0)
        {
            return false;
        }

        ShowWindow(hwnd, SwShow);
        SetForegroundWindow(hwnd);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
