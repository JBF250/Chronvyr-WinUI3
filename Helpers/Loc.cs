using Chronvyr.Services;

namespace Chronvyr.Helpers;

/// <summary>
/// 供 XAML 静态绑定使用的本地化入口。
/// DataTemplate 里的 x:Bind 绑定到数据项（如 WorkProject），无法访问页面的 <c>Loc</c> 属性，
/// 因此这些位置改用静态属性：<c>Text="{x:Bind helpers:Loc.LatestUpdate}"</c>。
/// </summary>
public static class Loc
{
    private static LocalizationService? Service => LocalizationService.Instance;

    private static string Get(string key) => Service?[key] ?? key;

    /// <summary>按 key 取文本（通用入口）。</summary>
    public static string T(string key) => Get(key);

    public static string LatestUpdate => Get("WorkbenchLatest");

    public static string Edit => Get("ActionEdit");

    public static string Delete => Get("ActionDelete");

    public static string MarkCompletedTip => Get("ScheduleMarkCompletedTip");

    public static string EditTip => Get("ScheduleEditTip");

    public static string DeleteTip => Get("ScheduleDeleteTip");

    public static string CopyTip => Get("ClipboardCopyTip");
}
