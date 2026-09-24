using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronvyr.Services;

/// <summary>
/// 本地 JSON 文件读写。采用「临时文件 + 原子替换」避免写入中断导致数据文件损坏，
/// 读取失败时返回 null 而不抛出（NFR-1.1 / NFR-1.2）。
/// </summary>
public static class JsonStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lock Gate = new();

    /// <summary>读取 JSON 文件；文件不存在或解析失败时返回 null。</summary>
    public static T? Load<T>(string path)
        where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<T>(json, ReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>原子写入 JSON 文件；失败时静默忽略（不打断用户操作）。</summary>
    public static bool Save<T>(string path, T value)
    {
        lock (Gate)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(value, WriteOptions);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, path, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
