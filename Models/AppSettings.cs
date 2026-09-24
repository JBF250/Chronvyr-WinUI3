using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronvyr.Models;

/// <summary>
/// 全局设置（FR-7）。持久化到 %LocalAppData%\Chronvyr\settings.json。
/// </summary>
public partial class AppSettings : ObservableObject
{
    private VisualStyle _mainStyle = VisualStyle.FollowSystem;
    private AppLanguage _language = AppLanguage.ChineseSimplified;
    private bool _autoStart;
    private DesktopWidgetSettings _diaryBar = new() { Kind = DesktopWidgetKind.DiaryBar };
    private DesktopWidgetSettings _scheduleBoard = new() { Kind = DesktopWidgetKind.ScheduleBoard };
    private DesktopWidgetSettings _dynamicIsland = new() { Kind = DesktopWidgetKind.DynamicIsland };

    /// <summary>主界面视觉风格（FR-7.1）。</summary>
    public VisualStyle MainStyle
    {
        get => _mainStyle;
        set => SetProperty(ref _mainStyle, value);
    }

    /// <summary>界面语言（FR-1.5）。</summary>
    public AppLanguage Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    /// <summary>开机自启（FR-7.3，注册表写入）。</summary>
    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    /// <summary>日记栏桌面控件设置（FR-7.2）。</summary>
    public DesktopWidgetSettings DiaryBar
    {
        get => _diaryBar;
        set => SetProperty(ref _diaryBar, value);
    }

    /// <summary>日程表桌面控件设置（FR-7.2）。</summary>
    public DesktopWidgetSettings ScheduleBoard
    {
        get => _scheduleBoard;
        set => SetProperty(ref _scheduleBoard, value);
    }

    /// <summary>胶囊栏桌面控件设置（FR-7.2）。</summary>
    public DesktopWidgetSettings DynamicIsland
    {
        get => _dynamicIsland;
        set => SetProperty(ref _dynamicIsland, value);
    }

    /// <summary>按控件类型取出对应设置。</summary>
    public DesktopWidgetSettings GetWidget(DesktopWidgetKind kind) => kind switch
    {
        DesktopWidgetKind.DiaryBar => DiaryBar,
        DesktopWidgetKind.ScheduleBoard => ScheduleBoard,
        _ => DynamicIsland,
    };
}

/// <summary>
/// 单个桌面控件的设置（FR-7.2）：是否创建、独立视觉风格、X/Y 显示位置。
/// </summary>
public partial class DesktopWidgetSettings : ObservableObject
{
    private bool _isEnabled;
    private bool _useMainStyle = true;
    private VisualStyle _style = VisualStyle.FollowSystem;
    private double _x;
    private double _y;
    private double _width;
    private double _topGap = 8;

    /// <summary>控件类型。</summary>
    public DesktopWidgetKind Kind { get; set; }

    /// <summary>是否在桌面创建该控件。</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>是否使用主界面风格；为 false 时使用 <see cref="Style"/>。</summary>
    public bool UseMainStyle
    {
        get => _useMainStyle;
        set => SetProperty(ref _useMainStyle, value);
    }

    /// <summary>该控件独立的视觉风格。</summary>
    public VisualStyle Style
    {
        get => _style;
        set
        {
            if (SetProperty(ref _style, value))
            {
                OnPropertyChanged(nameof(StyleIndex));
            }
        }
    }

    /// <summary>控件名称（用于设置界面标题）。</summary>
    [JsonIgnore]
    public string KindText => Kind switch
    {
        DesktopWidgetKind.DiaryBar => "日记栏",
        DesktopWidgetKind.ScheduleBoard => "日程表",
        _ => "胶囊栏",
    };

    /// <summary>视觉风格在设置下拉框中的索引。</summary>
    [JsonIgnore]
    public int StyleIndex
    {
        get => (int)Style;
        set
        {
            var next = (VisualStyle)Math.Clamp(value, (int)VisualStyle.FollowSystem, (int)VisualStyle.Dark);
            if (Style != next)
            {
                Style = next;
            }
        }
    }

    /// <summary>X 轴位置（屏幕逻辑像素，相对主屏左上角）。</summary>
    /// <remarks>
    /// 1.1 起设置页不再暴露这一项：日程表和日记栏靠拖动定位，胶囊栏的位置由
    /// <see cref="Width"/> 与 <see cref="TopGap"/> 算出来。字段保留是为了记住拖动结果。
    /// </remarks>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>Y 轴位置（屏幕逻辑像素，相对主屏左上角）。</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>胶囊栏宽度（逻辑像素）：收起与展开共用同一个值。</summary>
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    /// <summary>胶囊栏与屏幕顶部边缘之间的空隙（逻辑像素）。</summary>
    public double TopGap
    {
        get => _topGap;
        set => SetProperty(ref _topGap, value);
    }
}
