using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Chronvyr.Models;

namespace Chronvyr.Services;

/// <summary>
/// 中国节假日数据服务（FR-2.4）。
/// 联网时从 NateScarlet/holiday-cn 拉取并缓存到本地；离线时回退到缓存，均失败则返回空表。
/// </summary>
/// <remarks>
/// 数据源用多个镜像依次尝试，而不是绑死一个：
/// raw.githubusercontent.com 与 jsDelivr 各节点在不同地区、不同时段的可用性会此消彼长，
/// 实测遇到过 jsDelivr 连接被重置、而 raw.githubusercontent 反而通畅的情况。
/// </remarks>
public sealed class HolidayService
{
    /// <summary>
    /// 中国大陆专用数据源（含调休信息）。按顺序尝试，第一个成功的即采用。
    /// 注意：这几个镜像的可用性会随地区与时段此消彼长，实测遇到过 jsDelivr 连接被重置、
    /// 而 raw.githubusercontent 反而通畅的情况，所以不能只绑一个。
    /// </summary>
    private static readonly string[] ChinaUrlTemplates =
    [
        "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{0}.json",
        "https://cdn.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
        "https://fastly.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
        "https://gcore.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
    ];

    /// <summary>
    /// 其他地区的数据源（Nager.Date，覆盖 100+ 国家）。
    /// 之所以不给中国大陆用它：它只列法定节假日的正日子，**不含调休**，
    /// 实测 2026 年中国只返回 6 条，而 holiday-cn 有 39 条。
    /// </summary>
    private const string WorldUrlTemplate = "https://date.nager.at/api/v3/PublicHolidays/{0}/{1}";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 全局共享实例。CalendarPage 每次导航都会重建 ViewModel，
    /// 若各自 new 一个服务，内存缓存会随页面销毁而丢失。
    /// </summary>
    public static HolidayService Shared { get; } = new();

    private readonly Dictionary<int, Dictionary<DateOnly, HolidayDay>> _memory = [];

    /// <summary>
    /// 当前系统区域对应的国家代码（ISO 3166-1 alpha-2，大写）。
    /// 用户要的「按地区自动获取节假日」就是靠它实现的 ——
    /// Windows 并没有公开 API 能读取系统日历里的节假日（那是 Shell 的内部在线服务），
    /// 但系统区域是可靠可读的，据此选数据源即可达到同样效果。
    /// </summary>
    public static string CountryCode
    {
        get
        {
            try
            {
                var code = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;
                return string.IsNullOrWhiteSpace(code) ? "CN" : code.ToUpperInvariant();
            }
            catch (ArgumentException)
            {
                return "CN";
            }
        }
    }

    /// <summary>是否为使用 holiday-cn（含调休）的地区。</summary>
    private static bool IsChinaMainland => CountryCode == "CN";

    /// <summary>当前是否处于联网状态。</summary>
    public static bool IsOnline
    {
        get
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 获取指定年份的节假日表（DateOnly → HolidayDay）。
    /// 默认优先使用本地缓存；<paramref name="forceRefresh"/> 为 true 时强制联网重新拉取。
    /// </summary>
    public async Task<IReadOnlyDictionary<DateOnly, HolidayDay>> GetYearAsync(int year, bool forceRefresh = false)
    {
        if (!forceRefresh && _memory.TryGetValue(year, out var cached))
        {
            return cached;
        }

        var map = await LoadYearAsync(year, forceRefresh).ConfigureAwait(false);
        _memory[year] = map;
        return map;
    }

    private static async Task<Dictionary<DateOnly, HolidayDay>> LoadYearAsync(int year, bool forceRefresh)
    {
        AppPaths.EnsureCreated();
        // 缓存按「国家-年份」区分，避免用户改了系统区域后读到上一个国家的数据。
        var cacheFile = Path.Combine(AppPaths.HolidayCacheDir, $"{CountryCode}-{year}.json");

        // 缓存优先：拿到过一次就长期使用本地文件，不再每次进日历页都联网等待。
        // 之前是「联网优先」，导致每次切换到日历页都要等网络超时/往返。
        if (!forceRefresh && File.Exists(cacheFile))
        {
            try
            {
                var cached = Parse(await File.ReadAllTextAsync(cacheFile, Encoding.UTF8).ConfigureAwait(false));
                if (cached.Count > 0)
                {
                    return cached;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // 缓存损坏：继续走联网。
            }
        }

        if (IsOnline)
        {
            // 中国大陆走含调休的 holiday-cn（多镜像回退）；其他地区走 Nager.Date。
            var urls = IsChinaMainland
                ? ChinaUrlTemplates.Select(t => string.Format(System.Globalization.CultureInfo.InvariantCulture, t, year))
                : [string.Format(System.Globalization.CultureInfo.InvariantCulture, WorldUrlTemplate, year, CountryCode)];

            foreach (var url in urls)
            {
                try
                {
                    var json = await Http.GetStringAsync(url).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        TryWriteCache(cacheFile, json);
                        return Parse(json);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
                {
                    // 这个源不可用，继续试下一个。
                }
            }
        }

        // 联网失败（或强制刷新失败）时，退回到已损坏/过期的缓存也比空表好。
        try
        {
            if (File.Exists(cacheFile))
            {
                return Parse(await File.ReadAllTextAsync(cacheFile, Encoding.UTF8).ConfigureAwait(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 缓存不可读：返回空表。
        }

        return [];
    }

    private static void TryWriteCache(string path, string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 缓存写入失败不影响本次使用。
        }
    }

    private static Dictionary<DateOnly, HolidayDay> Parse(string json)
    {
        var map = new Dictionary<DateOnly, HolidayDay>();
        try
        {
            // 两种数据源格式不同，自动识别：
            //   holiday-cn —— 对象 { "year":..., "days":[{"name","date","isOffDay"}] }
            //   Nager.Date —— 数组 [{"date","localName","name",...}]，只列放假日
            if (json.TrimStart().StartsWith('['))
            {
                foreach (var item in JsonSerializer.Deserialize<List<NagerHoliday>>(json, ReadOptions) ?? [])
                {
                    if (DateOnly.TryParse(item.Date, CultureInfo.InvariantCulture, out var date))
                    {
                        var name = string.IsNullOrWhiteSpace(item.LocalName) ? item.Name : item.LocalName;
                        map[date] = new HolidayDay { Date = date, Name = name ?? string.Empty, IsOffDay = true };
                    }
                }

                return map;
            }

            var file = JsonSerializer.Deserialize<HolidayFile>(json, ReadOptions);
            if (file?.Days is null)
            {
                return map;
            }

            foreach (var day in file.Days)
            {
                map[day.Date] = day;
            }
        }
        catch (JsonException)
        {
            // 数据格式异常：返回已解析部分。
        }

        return map;
    }
    /// <summary>Nager.Date 的单条节假日记录（仅列放假日，无调休）。</summary>
    private sealed class NagerHoliday
    {
        public string? Date { get; set; }

        public string? LocalName { get; set; }

        public string? Name { get; set; }
    }
}