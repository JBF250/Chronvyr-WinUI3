using System.Runtime.InteropServices;

namespace Chronvyr.Helpers;

/// <summary>
/// 托盘右键/左键弹出菜单（FR-8.2）。
/// 使用 Win32 原生弹出菜单：主窗口隐藏时 XAML Flyout 无法弹出，原生菜单不受此限制。
/// </summary>
public static class TrayMenu
{
    /// <summary>菜单项。</summary>
    public enum TrayCommand
    {
        None = 0,
        OpenChronvyr = 2,
        OpenSettings = 3,
        Exit = 4,
    }

    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    /// <summary>显示托盘菜单并返回用户选择的命令。</summary>
    public static TrayCommand Show(nint ownerHwnd)
    {
        var menu = CreatePopupMenu();
        if (menu == 0)
        {
            return TrayCommand.None;
        }

        try
        {
            // 文案一律从本地化服务取：写死中文的话，英文 / 日文界面下
            // 菜单会突然冒出中文。
            var loc = App.Loc;
            _ = AppendMenu(menu, MfString, (nuint)TrayCommand.OpenChronvyr, loc["TrayOpen"]);
            _ = AppendMenu(menu, MfString, (nuint)TrayCommand.OpenSettings, loc["TraySettings"]);
            _ = AppendMenu(menu, MfSeparator, 0, null);
            _ = AppendMenu(menu, MfString, (nuint)TrayCommand.Exit, loc["TrayExit"]);

            if (!GetCursorPos(out var point))
            {
                return TrayCommand.None;
            }

            // 托盘菜单的标准做法：先让宿主窗口成为前台窗口，否则点击别处菜单不会消失。
            _ = SetForegroundWindow(ownerHwnd);

            var selected = TrackPopupMenu(
                menu,
                TpmLeftAlign | TpmRightButton | TpmReturnCmd,
                point.X,
                point.Y,
                0,
                ownerHwnd,
                0);

            return Enum.IsDefined(typeof(TrayCommand), (int)selected)
                ? (TrayCommand)selected
                : TrayCommand.None;
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(
        nint hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        nint hWnd,
        nint prcRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
