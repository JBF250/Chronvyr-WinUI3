namespace Chronvyr.Models;

/// <summary>
/// 视觉风格（FR-7.1）。
/// 早期版本尝试过液态玻璃与毛玻璃，但由于无法真正透视窗口后面的内容，
/// 效果等同于「把桌面壁纸当窗口背景」，反而压低了卡片与边框的对比度，故已移除。
/// 现在只保留三种主题，顺序与设置界面上的三个圆角卡片一致。
/// </summary>
public enum VisualStyle
{
    /// <summary>跟随系统：每次启动读取系统主题后自动应用。</summary>
    FollowSystem,

    /// <summary>浅色（Win11 上带 Mica 效果）。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}

/// <summary>
/// 桌面控件类型（FR-6 / FR-6.2）。
/// </summary>
public enum DesktopWidgetKind
{
    /// <summary>日记栏。</summary>
    DiaryBar,

    /// <summary>日程表。</summary>
    ScheduleBoard,

    /// <summary>胶囊栏。</summary>
    DynamicIsland,
}

/// <summary>界面语言（FR-7.4）。</summary>
public enum AppLanguage
{
    /// <summary>简体中文。</summary>
    ChineseSimplified,

    /// <summary>繁體中文。</summary>
    ChineseTraditional,

    /// <summary>English。</summary>
    English,

    /// <summary>日本語。</summary>
    Japanese,
}
