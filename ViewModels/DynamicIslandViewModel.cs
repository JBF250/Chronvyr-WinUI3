using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;
using Chronvyr.Services;
using Microsoft.UI.Xaml;

namespace Chronvyr.ViewModels;

/// <summary>
/// 桌面胶囊栏视图模型（FR-5.4）。
/// 收缩态展示：音频条、当天未完成日程数量、番茄钟倒计时、时间日期。
/// 展开面板展示：日程表、日记栏、番茄钟卡片、工作台卡片、硬件监控卡片、天气卡片、剪切板历史。
/// </summary>
public sealed class DynamicIslandViewModel : ObservableObject
{
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    private readonly PomodoroService _pomodoro;
    private readonly AudioLevelService _audio;
    private readonly HardwareMonitorService _hardware;
    private readonly WeatherService _weather;
    private readonly ObservableCollection<ScheduleItem> _schedules;
    private readonly ObservableCollection<DiaryEntry> _diaries;
    private readonly DispatcherTimer _clock;

    private readonly Queue<float> _levels = new();

    private string _timeText = "--:--";
    private string _dateText = string.Empty;
    private int _pendingCount;
    private string _pomodoroText = "--:--";
    private bool _isPomodoroActive;
    private bool _isExpanded;
    private int _tickCount;

    private double _bar1 = 5;
    private double _bar2 = 9;
    private double _bar3 = 6;
    private double _bar4 = 12;

    private double _cpuUsage;
    private double _memoryPercent;
    private string _memoryText = "--";
    private string _batteryText = "无电池";
    private bool _hasBattery;
    private int _batteryPercent;
    private bool _batteryCharging;
    private string _weatherText = "--";
    private string _weatherCity = "定位中…";
    private bool _hasWeather;

    public DynamicIslandViewModel(
        PomodoroService pomodoro,
        AudioLevelService audio,
        HardwareMonitorService hardware,
        WeatherService weather,
        ClipboardService clipboard,
        ObservableCollection<ScheduleItem> schedules,
        ObservableCollection<DiaryEntry> diaries,
        ObservableCollection<WorkProject> projects)
    {
        _pomodoro = pomodoro;
        _audio = audio;
        _hardware = hardware;
        _weather = weather;
        _schedules = schedules;
        _diaries = diaries;
        Projects = projects;
        Clipboard = clipboard;

        _pomodoro.StateChanged += (_, _) => RefreshPomodoro();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => OnTick();

        RefreshClock();
        RefreshAll();
        RefreshPomodoro();
        _clock.Start();
    }

    /// <summary>展开面板中的今日日程。</summary>
    /// <summary>全部日程（未完成在前，按截止时间升序）。</summary>
    public ObservableCollection<ScheduleItem> AllSchedules { get; } = [];

    /// <summary>展开面板中的最近日记。</summary>
    public ObservableCollection<DiaryEntry> RecentDiaries { get; } = [];

    /// <summary>展开面板中的工作台项目。</summary>
    public ObservableCollection<WorkProject> Projects { get; }

    /// <summary>剪切板历史服务（面板直接绑定其 History）。</summary>
    public ClipboardService Clipboard { get; }

    /// <summary>暂停时钟（窗口关闭时调用）。</summary>
    public void Stop()
    {
        _clock.Stop();
        _audio.Stop();
        Clipboard.Stop();
    }

    /// <summary>启动需要持续运行的服务。</summary>
    public void Start()
    {
        _audio.Start();
        Clipboard.Start();
        _ = RefreshWeatherAsync();
    }

    /// <summary>当前时间 HH:mm。</summary>
    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    /// <summary>日期文本。</summary>
    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    /// <summary>当天未完成日程数量。</summary>
    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (SetProperty(ref _pendingCount, value))
            {
                OnPropertyChanged(nameof(PendingText));
            }
        }
    }

    /// <summary>未完成日程数量文本。</summary>
    public string PendingText => App.Loc.Format("IslandPending", _pendingCount);

    /// <summary>番茄钟剩余时间。</summary>
    public string PomodoroText
    {
        get => _pomodoroText;
        private set => SetProperty(ref _pomodoroText, value);
    }

    /// <summary>番茄钟是否处于计时状态。</summary>
    public bool IsPomodoroActive
    {
        get => _isPomodoroActive;
        private set => SetProperty(ref _isPomodoroActive, value);
    }

    /// <summary>
    /// 窄条中部是否显示天气。
    /// 番茄钟运行时中部让位给倒计时，所以这里与 <see cref="IsPomodoroActive"/> 互斥。
    /// （Window 不是 FrameworkElement，XAML 里用不了 InverseBool 转换器。）
    /// </summary>
    public bool IsWeatherVisible => !_isPomodoroActive;

    /// <summary>
    /// 番茄钟主按钮的文字。
    /// 1.1 起把「开始专注」与「暂停」合并为同一个按钮，文字随状态切换，
    /// 不再并排放两个各自只在一半时间里有效的按钮。
    /// </summary>
    public string PomodoroPrimaryText => _pomodoro.State switch
    {
        PomodoroState.Running => App.Loc["PomodoroPause"],
        PomodoroState.Paused => App.Loc["PomodoroResume"],
        PomodoroState.Finished => App.Loc["PomodoroAgain"],
        _ => App.Loc["PomodoroStart"],
    };

    /// <summary>是否显示「退出专注」（仅在番茄钟已启动时有意义）。</summary>
    public bool CanExitPomodoro => _pomodoro.State != PomodoroState.Idle;

    /// <summary>面板是否已展开。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public double Bar1
    {
        get => _bar1;
        private set => SetProperty(ref _bar1, value);
    }

    public double Bar2
    {
        get => _bar2;
        private set => SetProperty(ref _bar2, value);
    }

    public double Bar3
    {
        get => _bar3;
        private set => SetProperty(ref _bar3, value);
    }

    public double Bar4
    {
        get => _bar4;
        private set => SetProperty(ref _bar4, value);
    }

    /// <summary>CPU 占用百分比。</summary>
    public double CpuUsage
    {
        get => _cpuUsage;
        private set
        {
            if (SetProperty(ref _cpuUsage, value))
            {
                OnPropertyChanged(nameof(CpuText));
            }
        }
    }

    private double _diskUsage;
    private double _gpuUsage;

    /// <summary>系统盘占用百分比。</summary>
    public double DiskUsage
    {
        get => _diskUsage;
        private set
        {
            if (SetProperty(ref _diskUsage, value))
            {
                OnPropertyChanged(nameof(DiskText));
            }
        }
    }

    /// <summary>系统盘占用展示文本。</summary>
    public string DiskText => $"{_diskUsage:F0}%";

    /// <summary>显卡占用百分比（所有 GPU 引擎实例之和）。</summary>
    public double GpuUsage
    {
        get => _gpuUsage;
        private set
        {
            if (SetProperty(ref _gpuUsage, value))
            {
                OnPropertyChanged(nameof(GpuText));
            }
        }
    }

    /// <summary>显卡占用展示文本。</summary>
    public string GpuText => $"{_gpuUsage:F0}%";

    /// <summary>CPU 文本。</summary>
    public string CpuText => $"{_cpuUsage:F0}%";

    /// <summary>内存占用百分比。</summary>
    public double MemoryPercent
    {
        get => _memoryPercent;
        private set => SetProperty(ref _memoryPercent, value);
    }

    /// <summary>内存详情文本。</summary>
    public string MemoryText
    {
        get => _memoryText;
        private set => SetProperty(ref _memoryText, value);
    }

    /// <summary>是否存在电池。</summary>
    public bool HasBattery
    {
        get => _hasBattery;
        private set => SetProperty(ref _hasBattery, value);
    }

    /// <summary>电池详情文本。</summary>
    public string BatteryText
    {
        get => _batteryText;
        private set => SetProperty(ref _batteryText, value);
    }

    /// <summary>电池图标（Segoe Fluent，按电量档位变化）。</summary>
    public string BatteryGlyph
    {
        get
        {
            if (!_hasBattery)
            {
                return "\uE83F"; // 无电池
            }

            if (_batteryCharging)
            {
                return "\uE85A"; // 充电中
            }

            // Battery0..Battery10 = E83F, E850..E859
            var level = Math.Clamp((int)Math.Round(_batteryPercent / 10.0), 0, 10);
            return level switch
            {
                0 => "\uE83F",
                1 => "\uE850",
                2 => "\uE851",
                3 => "\uE852",
                4 => "\uE853",
                5 => "\uE854",
                6 => "\uE855",
                7 => "\uE856",
                8 => "\uE857",
                9 => "\uE858",
                _ => "\uE859",
            };
        }
    }

    /// <summary>天气文本，如「21.5°C 晴」。</summary>
    public string WeatherText
    {
        get => _weatherText;
        private set => SetProperty(ref _weatherText, value);
    }

    /// <summary>天气城市。</summary>
    public string WeatherCity
    {
        get => _weatherCity;
        private set => SetProperty(ref _weatherCity, value);
    }

    /// <summary>是否已获取到天气。</summary>
    public bool HasWeather
    {
        get => _hasWeather;
        private set => SetProperty(ref _hasWeather, value);
    }

    /// <summary>今日日程为空。</summary>
    public bool IsAllSchedulesEmpty => AllSchedules.Count == 0;

    /// <summary>还没有日记。</summary>
    public bool IsDiariesEmpty => RecentDiaries.Count == 0;

    /// <summary>还没有项目。</summary>
    public bool IsProjectsEmpty => Projects.Count == 0;

    /// <summary>数据变化时刷新面板内容。</summary>
    public void RefreshAll()
    {
        // 显示全部日程（未完成在前），而不是只筛「今天」。
        // 之前只看 DueDate == today，导致新建的日程只要不是今天到期，
        // 胶囊栏里的日程表就一直空白，与桌面日程表控件的行为也不一致。
        var ordered = _schedules
            .OrderBy(item => item.IsCompleted)
            .ThenBy(item => item.DueDate)
            .ToList();

        AllSchedules.Clear();
        foreach (var item in ordered)
        {
            AllSchedules.Add(item);
        }

        // 窄条上的「待完成 N 日程」统计的是全部未完成项。
        PendingCount = _schedules.Count(item => !item.IsCompleted);
        OnPropertyChanged(nameof(IsAllSchedulesEmpty));

        RecentDiaries.Clear();
        foreach (var entry in _diaries.OrderByDescending(d => d.Date).ThenByDescending(d => d.CreatedAt).Take(6))
        {
            RecentDiaries.Add(entry);
        }

        OnPropertyChanged(nameof(IsDiariesEmpty));
        OnPropertyChanged(nameof(IsProjectsEmpty));
    }

    private void OnTick()
    {
        RefreshClock();

        // 音频条已移除：不再采样音频电平，省掉每秒数次的重绘与 WASAPI 环回开销。

        _tickCount++;
        if (_tickCount % 2 == 0)
        {
            RefreshHardware();
        }

        if (_tickCount % 1800 == 0)
        {
            _ = RefreshWeatherAsync();
        }
    }

    /// <summary>音频条最小变化阈值（像素）。小于它的抖动不触发界面重绘。</summary>
    private const double BarChangeThreshold = 0.6;

    private void RefreshAudioBars()
    {
        // 直接取四个频段的包络值：低音 → 高音分别驱动四条，
        // 不再像早期版本那样把同一个总峰值延迟铺开。
        var bands = _audio.SampleBands();

        SetBar(ref _bar1, ToBarHeight(bands, 0, 5), nameof(Bar1));
        SetBar(ref _bar2, ToBarHeight(bands, 1, 9), nameof(Bar2));
        SetBar(ref _bar3, ToBarHeight(bands, 2, 6), nameof(Bar3));
        SetBar(ref _bar4, ToBarHeight(bands, 3, 12), nameof(Bar4));
    }

    /// <summary>
    /// 带阈值的赋值：音频电平每帧都有微小浮点变化，若每次都通知界面，
    /// 桌面控件会以每秒数十次的频率重绘，进而打断 Wallpaper Engine 等
    /// 桌面层渲染。这里只在视觉上真的看得出差别时才更新。
    /// </summary>
    private void SetBar(ref double field, double value, string propertyName)
    {
        if (Math.Abs(field - value) < BarChangeThreshold)
        {
            return;
        }

        SetProperty(ref field, value, propertyName);
    }

    private static double ToBarHeight(float[] values, int index, double fallback)
    {
        if (index >= values.Length)
        {
            return fallback;
        }

        return 4 + (Math.Clamp(values[index], 0f, 1f) * 20);
    }

    private void RefreshHardware()
    {
        var snapshot = _hardware.Sample();

        CpuUsage = snapshot.CpuUsage;
        MemoryPercent = snapshot.MemoryUsagePercent;
        MemoryText = $"{snapshot.MemoryUsedGb:F1} / {snapshot.MemoryTotalGb:F1} GB";
        DiskUsage = snapshot.DiskUsagePercent;
        GpuUsage = snapshot.GpuUsagePercent;

        HasBattery = snapshot.HasBattery;
        _batteryPercent = snapshot.BatteryPercent;
        _batteryCharging = snapshot.IsCharging;

        var loc = App.Loc;
        BatteryText = snapshot.HasBattery
            ? $"{snapshot.BatteryPercent}% · {(snapshot.IsCharging ? loc["BatteryCharging"] : loc["BatteryDischarging"])}"
            : loc["BatteryNone"];

        OnPropertyChanged(nameof(BatteryGlyph));
    }

    private async Task RefreshWeatherAsync()
    {
        var info = await _weather.GetAsync().ConfigureAwait(true);
        if (info is null)
        {
            WeatherCity = WeatherService.UnavailableHint;
            HasWeather = false;
            return;
        }

        WeatherCity = info.City;
        WeatherText = $"{info.Temperature:F1}°C  {info.Description}";
        HasWeather = true;
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        TimeText = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DateText = now.ToString("M月d日 dddd", Zh);
    }

    private void RefreshPomodoro()
    {
        IsPomodoroActive = _pomodoro.State is PomodoroState.Running or PomodoroState.Paused;
        OnPropertyChanged(nameof(IsWeatherVisible));
        OnPropertyChanged(nameof(PomodoroPrimaryText));
        OnPropertyChanged(nameof(CanExitPomodoro));
        PomodoroText = _pomodoro.TimeText;
    }
}
