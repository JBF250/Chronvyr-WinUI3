namespace Chronvyr.Services;

/// <summary>
/// 应用数据路径（NFR-4：所有数据存于 %LocalAppData%\Chronvyr\）。
/// </summary>
public static class AppPaths
{
    /// <summary>数据根目录。</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chronvyr");

    /// <summary>日程数据文件。</summary>
    public static string SchedulesFile => Path.Combine(Root, "schedules.json");

    /// <summary>日记数据文件。</summary>
    public static string DiariesFile => Path.Combine(Root, "diaries.json");

    /// <summary>工作台项目数据文件。</summary>
    public static string ProjectsFile => Path.Combine(Root, "projects.json");

    /// <summary>设置文件。</summary>
    public static string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>节假日缓存目录。</summary>
    public static string HolidayCacheDir => Path.Combine(Root, "holidays");

    /// <summary>确保数据根目录存在。</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(HolidayCacheDir);
    }
}
