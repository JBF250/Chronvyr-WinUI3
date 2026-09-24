using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;
using Microsoft.UI.Windowing;

namespace Chronvyr.ViewModels;

/// <summary>
/// 设置页视图模型（FR-7）。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private string _styleHint = string.Empty;
    private string _autoStartHint = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        Settings = settings;

        Widgets =
        [
            settings.DiaryBar,
            settings.ScheduleBoard,
            settings.DynamicIsland,
        ];

        // 位置滑块上限取主显示器尺寸（物理像素）。
        try
        {
            var area = DisplayArea.Primary;
            ScreenWidth = area.OuterBounds.Width;
            ScreenHeight = area.OuterBounds.Height;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            ScreenWidth = 2560;
            ScreenHeight = 1440;
        }

        UpdateStyleHint();
    }

    /// <summary>应用设置对象。</summary>
    public AppSettings Settings { get; }

    /// <summary>三个桌面控件的设置（FR-7.2）。</summary>
    public ObservableCollection<DesktopWidgetSettings> Widgets { get; }

    /// <summary>主显示器宽度（物理像素）。</summary>
    public double ScreenWidth { get; }

    /// <summary>主显示器高度（物理像素）。</summary>
    public double ScreenHeight { get; }

    /// <summary>
    /// 胶囊栏宽度。设置里存 0 表示「还没调过」，这时回落到框架里的默认宽度，
    /// 免得滑块一打开就停在最小值上。
    /// </summary>
    public double DynamicIslandWidth
    {
        get => Settings.DynamicIsland.Width > 0
            ? Settings.DynamicIsland.Width
            : 360d;
        set
        {
            if (Math.Abs(Settings.DynamicIsland.Width - value) > 0.5)
            {
                Settings.DynamicIsland.Width = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>当前视觉风格的说明文字。</summary>
    public string StyleHint
    {
        get => _styleHint;
        private set => SetProperty(ref _styleHint, value);
    }

    /// <summary>开机自启操作反馈。</summary>
    public string AutoStartHint
    {
        get => _autoStartHint;
        set => SetProperty(ref _autoStartHint, value);
    }

    /// <summary>刷新风格说明。</summary>
    public void UpdateStyleHint()
    {
        var loc = App.Loc;
        var material = Services.ThemeService.IsMicaSupported ? "Mica" : "Acrylic";

        StyleHint = Settings.MainStyle switch
        {
            VisualStyle.Light => $"{loc["StyleLight"]} · {material}",
            VisualStyle.Dark => $"{loc["StyleDark"]} · {material}",
            _ => $"{loc["StyleFollowSystem"]} · {material}",
        };
    }

    /// <summary>是否选中「跟随系统」。</summary>
    public bool IsStyleFollowSystem => Settings.MainStyle == VisualStyle.FollowSystem;

    /// <summary>是否选中「浅色」。</summary>
    public bool IsStyleLight => Settings.MainStyle == VisualStyle.Light;

    /// <summary>是否选中「深色」。</summary>
    public bool IsStyleDark => Settings.MainStyle == VisualStyle.Dark;

    /// <summary>当前语言索引（0 中文 / 1 English / 2 日本語）。</summary>
    public int LanguageIndex
    {
        get => (int)Settings.Language;
        set
        {
            var clamped = Math.Clamp(value, 0, 3);
            if ((int)Settings.Language == clamped)
            {
                return;
            }

            Settings.Language = (AppLanguage)clamped;
            Services.LocalizationService.Apply(clamped);
            OnPropertyChanged();
            UpdateStyleHint();
        }
    }
}
