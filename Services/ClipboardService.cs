using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace Chronvyr.Services;

/// <summary>
/// 剪切板历史服务（FR-5.4 剪切板历史卡片）。
/// 轮询剪贴板文本，保留本次会话内的最近若干条记录（不落盘）。
/// </summary>
public sealed class ClipboardService : INotifyPropertyChanged
{
    private const int MaxItems = 20;

    private readonly DispatcherTimer _timer;
    private string _lastText = string.Empty;
    private bool _reading;
    private bool _initialized;

    public ClipboardService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _timer.Tick += async (_, _) => await PollAsync();

        History.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>剪切板历史（最新在前）。</summary>
    public ObservableCollection<string> History { get; } = [];

    /// <summary>是否为空。</summary>
    public bool IsEmpty => History.Count == 0;

    /// <summary>开始监听。</summary>
    public void Start()
    {
        // 不做同步读取（剪贴板 API 依赖 UI 线程，同步等待会死锁）；
        // 但也不能干等第一个定时器周期——那样用户在启动后立刻复制的第一条
        // 会被当成「基线」吞掉。这里立刻异步建立一次基线。
        _initialized = false;
        _ = PollAsync();
        _timer.Start();
    }

    /// <summary>停止监听。</summary>
    public void Stop() => _timer.Stop();

    private async Task PollAsync()
    {
        if (_reading)
        {
            return;
        }

        _reading = true;
        try
        {
            var text = await ReadTextAsync().ConfigureAwait(true);

            if (!_initialized)
            {
                // 建立基线：启动前就躺在剪贴板里的内容不算「刚复制」，不写入历史。
                // 注意即使是空内容也要把基线定下来，否则基线永远建立不了，
                // 用户启动后复制的第一条会被误当成基线吞掉。
                _initialized = true;
                _lastText = text ?? string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(text) || text == _lastText)
            {
                return;
            }

            _lastText = text;
            var trimmed = text.Length > 300 ? text[..300] + "…" : text;

            History.Remove(trimmed);
            History.Insert(0, trimmed);

            while (History.Count > MaxItems)
            {
                History.RemoveAt(History.Count - 1);
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // 剪贴板可能被其他程序占用，跳过本次。
        }
        finally
        {
            _reading = false;
        }
    }

    private static async Task<string> ReadTextAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            return string.Empty;
        }

        var text = await content.GetTextAsync();
        return text ?? string.Empty;
    }
}
