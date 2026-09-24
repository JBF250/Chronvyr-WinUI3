using System.Runtime.InteropServices;
using Chronvyr.Helpers;
using Chronvyr.Models;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Chronvyr.Services;

/// <summary>
/// 视觉风格与窗口外观服务（FR-7.1）。
/// 三种风格：跟随系统 / 浅色 / 深色。
/// 背景材质：Win11 上用 Mica，Win10 等不支持的环境自动降级为 Acrylic。
/// </summary>
public static class ThemeService
{
    // DWM 属性：让系统标题栏与窗口边框跟随应用主题（否则它们会一直是白色）。
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>当前风格设置。</summary>
    public static VisualStyle Current { get; private set; } = VisualStyle.FollowSystem;

    /// <summary>当前解析后的实际风格（不含「跟随系统」）。</summary>
    public static VisualStyle Resolved { get; private set; } = VisualStyle.Dark;

    /// <summary>Mica 是否可用（Win11 21H2 及以上）。</summary>
    public static bool IsMicaSupported => MicaController.IsSupported();

    /// <summary>把风格应用到指定窗口。</summary>
    public static void Apply(Window window, VisualStyle style)
    {
        Current = style;
        var resolved = SettingsService.Resolve(style);
        Resolved = resolved;
        var isLight = resolved == VisualStyle.Light;

        // 设置根元素的 RequestedTheme，整棵可视树会更换主题资源。
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        window.SystemBackdrop = CreateBackdrop(style);

        // 系统标题栏 / 窗口边框不随 XAML 主题变化，需要显式告知 DWM。
        ApplyWindowChrome(window, isLight);
    }

    /// <summary>让系统标题栏与窗口边框跟随应用主题。</summary>
    public static void ApplyWindowChrome(Window window, bool isLight)
    {
        try
        {
            var hwnd = WindowHelper.GetHandle(window);
            if (hwnd == 0)
            {
                return;
            }

            var dark = isLight ? 0 : 1;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                // 早期 Windows 10 使用 19。
                _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));
            }

            // Win11 圆角（DWMWCP_ROUND）。
            var corner = 2;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>只设置沉浸式深色模式（不碰圆角）。</summary>
    public static void SetImmersiveDarkMode(Window window, bool isLight)
    {
        try
        {
            var hwnd = WindowHelper.GetHandle(window);
            if (hwnd == 0)
            {
                return;
            }

            var dark = isLight ? 0 : 1;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>隐藏 DWM 绘制的窗口描边，避免深色主题下出现一圈浅色细线。</summary>
    public static void SetBorderInvisible(Window window)
    {
        try
        {
            var hwnd = WindowHelper.GetHandle(window);
            if (hwnd == 0)
            {
                return;
            }

            var colorNone = unchecked((int)0xFFFFFFFE);
            _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref colorNone, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>
    /// 关闭 DWM 自带的窗口圆角。
    /// 桌面控件改用与卡片一致的窗口区域裁剪，若两者同时生效会出现双层圆角。
    /// </summary>
    public static void SetWindowCornerNone(Window window)
    {
        try
        {
            var hwnd = WindowHelper.GetHandle(window);
            if (hwnd == 0)
            {
                return;
            }

            var corner = 1; // DWMWCP_DONOTROUND
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>按风格创建窗口背景材质。</summary>
    public static SystemBackdrop CreateBackdrop(VisualStyle style)
    {
        var resolved = SettingsService.Resolve(style);

        if (IsMicaSupported)
        {
            return new MicaBackdrop
            {
                Kind = resolved == VisualStyle.Light ? MicaKind.BaseAlt : MicaKind.Base,
            };
        }

        // Win10 等不支持 Mica 的环境降级为 Acrylic。
        return new DesktopAcrylicBackdrop();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
