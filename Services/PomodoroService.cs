using Microsoft.UI.Xaml;

namespace Chronvyr.Services;

/// <summary>番茄钟运行状态。</summary>
public enum PomodoroState
{
    /// <summary>待开始。</summary>
    Idle,

    /// <summary>专注中。</summary>
    Running,

    /// <summary>已暂停。</summary>
    Paused,

    /// <summary>已完成。</summary>
    Finished,
}

/// <summary>
/// 全局番茄钟服务（FR-5）：主界面番茄钟页与桌面胶囊栏共享同一份计时状态。
/// </summary>
public sealed class PomodoroService
{
    private readonly DispatcherTimer _timer;

    private TimeSpan _remaining;
    private TimeSpan _total;
    private PomodoroState _state = PomodoroState.Idle;
    private string _taskName = string.Empty;
    private double _focusMinutes = 25;

    public PomodoroService()
    {
        _total = TimeSpan.FromMinutes(_focusMinutes);
        _remaining = _total;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    /// <summary>状态或剩余时间发生变化。</summary>
    public event EventHandler? StateChanged;

    /// <summary>一轮专注结束。</summary>
    public event EventHandler? Finished;

    /// <summary>当前任务名（FR-5.1）。</summary>
    public string TaskName
    {
        get => _taskName;
        set
        {
            if (_taskName == value)
            {
                return;
            }

            _taskName = value;
            RaiseStateChanged();
        }
    }

    /// <summary>专注时长（分钟）。</summary>
    public double FocusMinutes
    {
        get => _focusMinutes;
        set
        {
            var clamped = Math.Clamp(value, 1, 180);
            if (Math.Abs(_focusMinutes - clamped) < 0.001)
            {
                return;
            }

            _focusMinutes = clamped;
            if (_state is PomodoroState.Idle or PomodoroState.Finished)
            {
                Reset();
            }
            else
            {
                RaiseStateChanged();
            }
        }
    }

    /// <summary>当前状态。</summary>
    public PomodoroState State => _state;

    /// <summary>剩余时间。</summary>
    public TimeSpan Remaining => _remaining;

    /// <summary>剩余时间文本 mm:ss。</summary>
    public string TimeText => $"{(int)_remaining.TotalMinutes:D2}:{_remaining.Seconds:D2}";

    /// <summary>进度（0-100）。</summary>
    public double Progress =>
        _total.TotalSeconds <= 0 ? 0 : (1 - (_remaining.TotalSeconds / _total.TotalSeconds)) * 100;

    /// <summary>开始或继续。</summary>
    public void Start()
    {
        if (_state == PomodoroState.Finished)
        {
            Reset();
        }

        _state = PomodoroState.Running;
        _timer.Start();
        RaiseStateChanged();
    }

    /// <summary>暂停。</summary>
    public void Pause()
    {
        if (_state != PomodoroState.Running)
        {
            return;
        }

        _timer.Stop();
        _state = PomodoroState.Paused;
        RaiseStateChanged();
    }

    /// <summary>重置到初始时长。</summary>
    public void Reset()
    {
        _timer.Stop();
        _state = PomodoroState.Idle;
        _total = TimeSpan.FromMinutes(_focusMinutes);
        _remaining = _total;
        RaiseStateChanged();
    }

    private void OnTick(object? sender, object e)
    {
        _remaining -= TimeSpan.FromSeconds(1);
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _timer.Stop();
            _state = PomodoroState.Finished;
            RaiseStateChanged();
            Finished?.Invoke(this, EventArgs.Empty);
            return;
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
