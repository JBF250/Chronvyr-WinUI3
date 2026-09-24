using System.Text;

namespace Chronvyr.Services;

/// <summary>
/// 轻量日志：写入 %LocalAppData%\Chronvyr\logs\chronvyr.log，用于诊断运行时异常（NFR-1.1）。
/// 日志失败绝不影响主流程。
/// </summary>
public static class Log
{
    private static readonly Lock Gate = new();

    /// <summary>日志文件路径。</summary>
    public static string FilePath => Path.Combine(AppPaths.Root, "logs", "chronvyr.log");

    /// <summary>写入一条日志。</summary>
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(
                    FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 忽略日志写入失败。
        }
    }

    /// <summary>写入一条异常日志（含内部异常链与堆栈）。</summary>
    public static void WriteException(string context, Exception? exception)
    {
        if (exception is null)
        {
            Write($"{context}: (null exception)");
            return;
        }

        var builder = new StringBuilder();
        builder.Append(context).Append(": ").Append(exception.GetType().FullName).Append(" - ").Append(exception.Message);

        var inner = exception.InnerException;
        var depth = 0;
        while (inner is not null && depth < 5)
        {
            builder.Append(Environment.NewLine).Append("  -> inner: ").Append(inner.GetType().FullName).Append(" - ").Append(inner.Message);
            inner = inner.InnerException;
            depth++;
        }

        builder.Append(Environment.NewLine).Append(exception.StackTrace);
        Write(builder.ToString());
    }
}
