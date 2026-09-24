using Chronvyr.Models;
using Microsoft.Win32;
using Microsoft.UI.Dispatching;

namespace Chronvyr.Services;

/// <summary>
/// 设置服务：加载/保存 <see cref="AppSettings"/>。
/// 变更采用防抖写入，避免拖动滑块时频繁落盘。
/// </summary>
public sealed class SettingsService
{
    private const int SaveDebounceMs = 400;

    private readonly AppSettings _settings;
    private readonly DispatcherQueueTimer? _saveTimer;

    public SettingsService()
    {
        AppPaths.EnsureCreated();
        _settings = LoadSettings();

        _settings.PropertyChanged += (_, _) => ScheduleSave();
        _settings.DiaryBar.PropertyChanged += (_, _) => ScheduleSave();
        _settings.ScheduleBoard.PropertyChanged += (_, _) => ScheduleSave();
        _settings.DynamicIsland.PropertyChanged += (_, _) => ScheduleSave();

        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue is not null)
        {
            _saveTimer = queue.CreateTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(SaveDebounceMs);
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += (_, _) => Save();
        }
    }

    /// <summary>当前设置。</summary>
    public AppSettings Current => _settings;

    /// <summary>
    /// 读取设置。早期版本存过已移除的风格值（LiquidGlass / Acrylic），
    /// 而枚举里已经没有它们会让整份 JSON 反序列化失败、连带丢掉全部设置，
    /// 因此先做一次兼容替换再解析。
    /// </summary>
    private static AppSettings LoadSettings()
    {
        try
        {
            var path = AppPaths.SettingsFile;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                if (json.Contains("LiquidGlass", StringComparison.Ordinal) || json.Contains("Acrylic", StringComparison.Ordinal))
                {
                    json = json
                        .Replace("\"LiquidGlass\"", "\"FollowSystem\"", StringComparison.Ordinal)
                        .Replace("\"Acrylic\"", "\"FollowSystem\"", StringComparison.Ordinal);
                    File.WriteAllText(path, json);
                    Log.Write("已把旧版视觉风格值迁移为「跟随系统」");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 迁移失败不影响后续读取。
        }

        return JsonStore.Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
    }

    /// <summary>立即写入磁盘。</summary>
    public void Save() => JsonStore.Save(AppPaths.SettingsFile, _settings);

    private void ScheduleSave()
    {
        if (_saveTimer is null)
        {
            Save();
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// 计算实际生效的视觉风格（处理「跟随系统」：每次启动检查一次系统主题，FR-7.1）。
    /// </summary>
    public static VisualStyle Resolve(VisualStyle style) =>
        style == VisualStyle.FollowSystem
            ? (IsSystemLightTheme() ? VisualStyle.Light : VisualStyle.Dark)
            : style;

    /// <summary>计算某个桌面控件实际生效的视觉风格。</summary>
    public VisualStyle ResolveWidget(DesktopWidgetSettings widget) =>
        Resolve(widget.UseMainStyle ? _settings.MainStyle : widget.Style);

    /// <summary>系统当前是否为浅色主题（注册表读取，免 WinRT 依赖）。</summary>
    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value != 0;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // 读不到时按深色处理。
        }

        return false;
    }
}
