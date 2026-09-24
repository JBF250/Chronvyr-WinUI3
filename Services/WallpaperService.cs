using Microsoft.Win32;

namespace Chronvyr.Services;

/// <summary>
/// 桌面壁纸服务：给液态玻璃提供可折射的背景。
/// 桌面壁纸路径存在注册表里；如果用户用动态壁纸（如 Wallpaper Engine），
/// 取不到静态文件，则由界面退回自带的渐变背景。
/// </summary>
public static class WallpaperService
{
    /// <summary>取当前桌面壁纸文件路径；取不到返回 null。</summary>
    public static string? GetWallpaperPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (key?.GetValue("Wallpaper") is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // 读不到就回退渐变背景。
        }

        return null;
    }
}
