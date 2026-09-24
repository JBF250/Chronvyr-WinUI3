using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Chronvyr.Services;

namespace Chronvyr.ViewModels;

/// <summary>
/// 番茄钟页视图模型（FR-5），包装全局 <see cref="PomodoroService"/>。
/// 说明：专注时长默认 25 分钟、可在界面调整；是否需要休息阶段与结束提醒方式待用户确认后补充。
/// </summary>
public sealed class PomodoroViewModel : ObservableObject
{
    private readonly PomodoroService _service;

    public PomodoroViewModel(PomodoroService service)
    {
        _service = service;
        _service.StateChanged += OnServiceStateChanged;
        _service.Finished += (_, _) => Finished?.Invoke(this, EventArgs.Empty);

        StartCommand = new RelayCommand(_service.Start, () => _service.State is PomodoroState.Idle or PomodoroState.Paused or PomodoroState.Finished);
        PauseCommand = new RelayCommand(_service.Pause, () => _service.State == PomodoroState.Running);
        ResetCommand = new RelayCommand(_service.Reset);
    }

    /// <summary>一轮专注结束（由全局服务转发）。</summary>
    public event EventHandler? Finished;

    public IRelayCommand StartCommand { get; }

    public IRelayCommand PauseCommand { get; }

    public IRelayCommand ResetCommand { get; }

    /// <summary>当前任务名（FR-5.1）。</summary>
    public string TaskName
    {
        get => _service.TaskName;
        set
        {
            if (_service.TaskName != value)
            {
                _service.TaskName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>专注时长（分钟）。</summary>
    public double FocusMinutes
    {
        get => _service.FocusMinutes;
        set
        {
            if (Math.Abs(_service.FocusMinutes - value) > 0.001)
            {
                _service.FocusMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    public string TimeText => _service.TimeText;

    public double Progress => _service.Progress;

    public string StateText => _service.State switch
    {
        PomodoroState.Running => "专注中",
        PomodoroState.Paused => "已暂停",
        PomodoroState.Finished => "本轮完成",
        _ => "准备开始",
    };

    public bool IsIdle => _service.State == PomodoroState.Idle;

    public bool IsRunning => _service.State == PomodoroState.Running;

    public bool IsPaused => _service.State == PomodoroState.Paused;

    public bool IsFinished => _service.State == PomodoroState.Finished;

    private void OnServiceStateChanged(object? sender, EventArgs e) => RaiseAll();

    private void RaiseAll()
    {        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsFinished));
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
    }
}
