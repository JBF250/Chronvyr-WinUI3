using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Chronvyr.Models;
using Chronvyr.Services;

namespace Chronvyr.ViewModels;

/// <summary>
/// 日历页视图模型（FR-2）：月视图、月份切换、日程标记、当日日程、节假日。
/// </summary>
public sealed class CalendarViewModel : ObservableObject
{
    /// <summary>当前语言对应的日期格式文化。</summary>
    private static CultureInfo CurrentCulture => App.Loc?.Language switch
    {
        AppLanguage.ChineseTraditional => CultureInfo.GetCultureInfo("zh-TW"),
        AppLanguage.English => CultureInfo.GetCultureInfo("en-US"),
        AppLanguage.Japanese => CultureInfo.GetCultureInfo("ja-JP"),
        _ => CultureInfo.GetCultureInfo("zh-CN"),
    };

    private readonly HolidayService _holidayService;
    private readonly ObservableCollection<ScheduleItem> _schedules;

    private DateTime _currentMonth;
    private DateOnly _selectedDate;
    private Dictionary<DateOnly, HolidayDay> _holidayMap = [];
    private string _holidayNotice = string.Empty;
    private string _weatherText = "--";
    private string _weatherCity = string.Empty;
    private bool _isBusy;

    public CalendarViewModel(HolidayService holidayService, ObservableCollection<ScheduleItem> schedules)
    {
        _holidayService = holidayService;
        _schedules = schedules;

        var today = DateTime.Today;
        _currentMonth = new DateTime(today.Year, today.Month, 1);
        _selectedDate = DateOnly.FromDateTime(today);

        PreviousMonthCommand = new RelayCommand(() => ShiftMonth(-1));
        NextMonthCommand = new RelayCommand(() => ShiftMonth(1));
        TodayCommand = new RelayCommand(GoToday);
        RefreshHolidaysCommand = new RelayCommand(OnRefreshHolidays);
        SelectDayCommand = new RelayCommand<CalendarDayViewModel>(SelectDay);

        BuildMonth();
        RefreshDaySchedules();
    }

    /// <summary>当月所有日期格（固定 6 周 = 42 格）。</summary>
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    /// <summary>月份网格重建后触发（视图据此重新生成日期单元格）。</summary>
    public event EventHandler? MonthRebuilt;

    /// <summary>选中日期的日程列表。</summary>
    public ObservableCollection<ScheduleItem> SelectedSchedules { get; } = [];

    public IRelayCommand PreviousMonthCommand { get; }

    public IRelayCommand NextMonthCommand { get; }

    public IRelayCommand TodayCommand { get; }

    /// <summary>
    /// 手动重新拉取节假日数据。
    /// 平时进入日历页直接用本地缓存（不再联网等待），只有点这个按钮才强制联网刷新。
    /// </summary>
    public IRelayCommand RefreshHolidaysCommand { get; }

    public IRelayCommand<CalendarDayViewModel> SelectDayCommand { get; }

    /// <summary>月份标题，如「2026年9月」。</summary>
    public string MonthTitle => $"{_currentMonth.Year}年{_currentMonth.Month}月";

    /// <summary>选中日期标题（日期 + 星期 + 节假日），随语言变化。</summary>
    public string SelectedDateTitle
    {
        get
        {
            var culture = CurrentCulture;
            var date = _selectedDate.ToDateTime(TimeOnly.MinValue);
            var text = date.ToString("D", culture);

            // en-US 的长日期格式本身已含星期，中日文需要额外拼上。
            if (culture.Name != "en-US")
            {
                text = $"{text} {date.ToString("dddd", culture)}";
            }

            if (!_holidayMap.TryGetValue(_selectedDate, out var holiday))
            {
                return text;
            }

            var badge = holiday.IsOffDay ? App.Loc?["HolidayRest"] : App.Loc?["HolidayMakeup"];
            return $"{text} · {holiday.Name}（{badge}）";
        }
    }

    /// <summary>选中日期是否没有日程。</summary>
    public bool IsSelectedEmpty => SelectedSchedules.Count == 0;

    /// <summary>节假日数据状态提示。</summary>
    public string HolidayNotice
    {
        get => _holidayNotice;
        private set => SetProperty(ref _holidayNotice, value);
    }

    /// <summary>是否正在加载节假日数据。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>天气文本，如「20.8°C 晴间多云」。</summary>
    public string WeatherText
    {
        get => _weatherText;
        private set => SetProperty(ref _weatherText, value);
    }

    /// <summary>天气所在城市。</summary>
    public string WeatherCity
    {
        get => _weatherCity;
        private set => SetProperty(ref _weatherCity, value);
    }

    /// <summary>页面加载时调用：拉取节假日与天气，重建视图。</summary>
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await EnsureHolidayDataAsync();
        }
        finally
        {
            IsBusy = false;
        }

        BuildMonth();
        RefreshDaySchedules();

        await RefreshWeatherAsync();
    }

    private async void OnRefreshHolidays()
    {
        IsBusy = true;
        try
        {
            HolidayNotice = string.Empty;
            await EnsureHolidayDataAsync(forceRefresh: true);
            BuildMonth();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshWeatherAsync()
    {
        if (App.Weather is null)
        {
            return;
        }

        var info = await App.Weather.GetAsync().ConfigureAwait(true);
        if (info is null)
        {
            WeatherCity = WeatherService.UnavailableHint;
            return;
        }

        WeatherText = $"{info.Temperature:F1}°C {info.Description}";
        WeatherCity = info.City;
    }

    /// <summary>日程数据变化时刷新日历上的标记。</summary>
    public void RefreshScheduleMarks()
    {
        var dates = new HashSet<DateOnly>();
        // 跨天日程要把开始日到截止日之间的每一天都标上，而不是只标截止日。
        foreach (var item in _schedules)
        {
            foreach (var date in EnumerateCoveredDates(item))
            {
                dates.Add(date);
            }
        }

        foreach (var day in Days)
        {
            day.HasSchedule = dates.Contains(day.Date);
        }

        RefreshDaySchedules();
    }

    /// <summary>选中某一天（FR-2.3）。</summary>
    public void SelectDay(CalendarDayViewModel? day)
    {
        if (day is null)
        {
            return;
        }

        _selectedDate = day.Date;

        // 点击非本月日期时切换到该月。
        if (!day.IsCurrentMonth)
        {
            _currentMonth = new DateTime(day.Date.Year, day.Date.Month, 1);
            OnPropertyChanged(nameof(MonthTitle));
            BuildMonth();
        }
        else
        {
            foreach (var item in Days)
            {
                item.IsSelected = item.Date == _selectedDate;
            }
        }

        RefreshDaySchedules();
    }

    private void ShiftMonth(int delta)
    {
        _currentMonth = _currentMonth.AddMonths(delta);
        OnPropertyChanged(nameof(MonthTitle));
        BuildMonth();
        RefreshDaySchedules();
    }

    private void GoToday()
    {
        var today = DateTime.Today;
        _currentMonth = new DateTime(today.Year, today.Month, 1);
        _selectedDate = DateOnly.FromDateTime(today);
        OnPropertyChanged(nameof(MonthTitle));
        BuildMonth();
        RefreshDaySchedules();
    }

    private async Task EnsureHolidayDataAsync(bool forceRefresh = false)
    {
        var first = new DateOnly(_currentMonth.Year, _currentMonth.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7; // 以周一为一周起始
        var gridStart = first.AddDays(-offset);
        var gridEnd = gridStart.AddDays(41);

        var years = new HashSet<int> { gridStart.Year, gridEnd.Year };
        var merged = new Dictionary<DateOnly, HolidayDay>();
        var offline = false;

        foreach (var year in years)
        {
            var map = await _holidayService.GetYearAsync(year, forceRefresh).ConfigureAwait(true);
            foreach (var pair in map)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        if (!HolidayService.IsOnline)
        {
            offline = true;
        }

        _holidayMap = merged;
        HolidayNotice = offline
            ? "当前离线，节假日为本地缓存数据"
            : merged.Count == 0
                ? "未能获取节假日数据"
                : string.Empty;
    }

    private void BuildMonth()
    {
        var existingSchedules = new HashSet<DateOnly>();
        foreach (var item in _schedules)
        {
            foreach (var date in EnumerateCoveredDates(item))
            {
                existingSchedules.Add(date);
            }
        }

        var first = new DateOnly(_currentMonth.Year, _currentMonth.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var gridStart = first.AddDays(-offset);

        Days.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            _holidayMap.TryGetValue(date, out var holiday);
            Days.Add(new CalendarDayViewModel(
                date,
                _currentMonth.Month,
                existingSchedules.Contains(date),
                date == _selectedDate,
                holiday));
        }

        MonthRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshDaySchedules()
    {
        SelectedSchedules.Clear();
        foreach (var item in _schedules)
        {
            if (item.CoversDate(_selectedDate))
            {
                SelectedSchedules.Add(item);
            }
        }

        OnPropertyChanged(nameof(SelectedDateTitle));
        OnPropertyChanged(nameof(IsSelectedEmpty));
    }
    /// <summary>
    /// 枚举一条日程覆盖的所有日期（含首尾）。
    /// 单日日程只产出一天；跨天日程产出开始日到截止日之间的连续日期。
    /// </summary>
    private static IEnumerable<DateOnly> EnumerateCoveredDates(ScheduleItem item)
    {
        for (var date = item.EffectiveStartDate; date <= item.DueDate; date = date.AddDays(1))
        {
            yield return date;
        }
    }
}