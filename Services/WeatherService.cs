using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronvyr.Services;

/// <summary>天气信息。</summary>
public sealed record WeatherInfo(string City, double Temperature, string Description);

/// <summary>
/// 天气服务（FR-5.4 天气卡片）。
/// 数据源：Open-Meteo（免 API Key）；定位：ipapi.co，失败时回退 ip-api.com。
/// 离线或请求失败时返回上一次的缓存，完全不阻塞界面。
/// </summary>
public sealed class WeatherService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private WeatherInfo? _cache;
    private DateTime _cacheAt = DateTime.MinValue;

    /// <summary>是否已有可用数据。</summary>
    public bool HasData => _cache is not null;

    /// <summary>天气不可用时的提示文本。</summary>
    public static string UnavailableHint => HolidayService.IsOnline ? "天气获取失败" : "离线，天气不可用";

    /// <summary>获取天气（带缓存）。</summary>
    public async Task<WeatherInfo?> GetAsync(bool force = false)
    {
        if (!force && _cache is not null && DateTime.Now - _cacheAt < CacheLifetime)
        {
            return _cache;
        }

        if (!HolidayService.IsOnline)
        {
            return _cache;
        }

        try
        {
            var location = await ResolveLocationAsync().ConfigureAwait(false);
            if (location is null)
            {
                return _cache;
            }

            var (city, latitude, longitude) = location.Value;
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://api.open-meteo.com/v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&current=temperature_2m,weather_code");

            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return _cache;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<OpenMeteoResponse>(json);
            if (payload?.Current is null)
            {
                return _cache;
            }

            _cache = new WeatherInfo(
                city,
                Math.Round(payload.Current.Temperature, 1),
                DescribeCode(payload.Current.WeatherCode));
            _cacheAt = DateTime.Now;
            return _cache;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return _cache;
        }
    }

    /// <summary>
    /// 解析当前所在城市与经纬度。
    /// </summary>
    /// <remarks>
    /// 依次尝试多个免费 IP 定位源。这类服务的可用性随地区与时段变化极大：
    /// 实测遇到过 ipapi.co 返回 403、ip-api.com 完全超时，而 ipinfo.io 正常的情况，
    /// 而旧实现恰好只用了前两个，于是天气永远取不到。
    /// 各源字段名不统一（ipinfo.io 把经纬度塞在 "loc":"lat,lon" 字符串里），
    /// 因此用 JsonDocument 动态取值，而不是为每家写一个强类型响应类。
    /// </remarks>
    private static async Task<(string City, double Latitude, double Longitude)?> ResolveLocationAsync()
    {
        foreach (var url in LocationUrls)
        {
            try
            {
                var json = await Http.GetStringAsync(url).ConfigureAwait(false);
                if (ParseLocation(json) is { } hit)
                {
                    return hit;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
            {
                // 这个源不可用，继续试下一个。
            }
        }

        return null;
    }

    /// <summary>候选定位源，按实测可用性排序。</summary>
    private static readonly string[] LocationUrls =
    [
        "https://ipinfo.io/json",
        "https://ipwho.is/",
        "https://freeipapi.com/api/json",
        "https://ipapi.co/json/",
        "http://ip-api.com/json/",
    ];

    /// <summary>从任意一家定位源的返回里取出城市与经纬度。</summary>
    private static (string City, double Latitude, double Longitude)? ParseLocation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var latitude = ReadDouble(root, "latitude");
        var longitude = ReadDouble(root, "longitude");

        // ipinfo.io 把经纬度放在 "loc": "30.5833,114.2667"
        if (latitude == 0 && longitude == 0
            && root.TryGetProperty("loc", out var locElement)
            && locElement.ValueKind == JsonValueKind.String)
        {
            var parts = locElement.GetString()?.Split(',');
            if (parts is { Length: 2 })
            {
                _ = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);
                _ = double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
            }
        }

        if (latitude == 0 && longitude == 0)
        {
            return null;
        }

        var city = ReadString(root, "city") ?? ReadString(root, "cityName") ?? "当前位置";
        return (city, latitude, longitude);
    }

    private static double ReadDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
    /// <summary>WMO 天气代码 → 中文描述。</summary>
    private static string DescribeCode(int code) => code switch
    {
        0 => "晴",
        1 => "晴间多云",
        2 => "局部多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 => "小雪",
        73 => "中雪",
        75 => "大雪",
        77 => "雪粒",
        80 or 81 => "阵雨",
        82 => "强阵雨",
        85 or 86 => "阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷暴伴冰雹",
        _ => "未知",
    };

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public CurrentBlock? Current { get; set; }
    }

    private sealed class CurrentBlock
    {
        [JsonPropertyName("temperature_2m")]
        public double Temperature { get; set; }

        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }
    }

    private sealed class IpApiCoResponse
    {
        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    private sealed class IpApiComResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
    }
}
