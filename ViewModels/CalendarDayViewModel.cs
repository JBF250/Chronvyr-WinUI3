using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>
/// 日历中单个日期的视图模型（FR-2.1 / FR-2.2）。
/// 所有视觉差异都通过布尔可见性与透明度表达，避免在视图模型里直接持有画刷，
/// 从而自动跟随主题切换。
/// </summary>
public sealed partial class CalendarDayViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _hasSchedule;

    public CalendarDayViewModel(
        DateOnly date,
        int currentMonth,
        bool hasSchedule,
        bool isSelected,
        HolidayDay? holiday)
    {
        Date = date;
        DayText = date.Day.ToString(System.Globalization.CultureInfo.InvariantCulture);
        IsCurrentMonth = date.Month == currentMonth;
        IsToday = date == DateOnly.FromDateTime(DateTime.Today);
        Holiday = holiday;
        _hasSchedule = hasSchedule;
        _isSelected = isSelected;
    }

    /// <summary>日期。</summary>
    public DateOnly Date { get; }

    /// <summary>日期数字文本。</summary>
    public string DayText { get; }

    /// <summary>是否属于当前显示的月份。</summary>
    public bool IsCurrentMonth { get; }

    /// <summary>是否为今天。</summary>
    public bool IsToday { get; }

    /// <summary>节假日/调休信息（可能为空）。</summary>
    public HolidayDay? Holiday { get; }

    /// <summary>法定放假日。</summary>
    public bool IsOffDay => Holiday is { IsOffDay: true };

    /// <summary>调休上班日。</summary>
    public bool IsWorkday => Holiday is { IsOffDay: false };

    /// <summary>「休息」徽章文本（随语言变化）。</summary>
    public static string RestLabel => Services.LocalizationService.Instance?["HolidayRest"] ?? "休息";

    /// <summary>「调休」徽章文本（随语言变化）。</summary>
    public static string MakeupLabel => Services.LocalizationService.Instance?["HolidayMakeup"] ?? "调休";

    /// <summary>休息徽章提示。</summary>
    public static string RestTip => Services.LocalizationService.Instance?["HolidayTipOff"] ?? "法定放假日";

    /// <summary>调休徽章提示。</summary>
    public static string MakeupTip => Services.LocalizationService.Instance?["HolidayTipWork"] ?? "调休上班日（周末补班）";

    /// <summary>节假日名称（用于提示气泡）。</summary>
    public string HolidayName => Holiday?.Name ?? string.Empty;

    /// <summary>提示气泡文本。</summary>
    public string TooltipText => Holiday is null
        ? Date.ToString("yyyy年M月d日", System.Globalization.CultureInfo.InvariantCulture)
        : $"{Date:yyyy年M月d日} · {Holiday.Name}{(Holiday.IsOffDay ? "（休）" : "（班）")}";

    /// <summary>非本月的日期做淡化处理。</summary>
    public double CellOpacity => IsCurrentMonth ? 1d : 0.38d;

    /// <summary>是否被选中。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>当天是否存在日程（FR-2.2 标记）。</summary>
    public bool HasSchedule
    {
        get => _hasSchedule;
        set => SetProperty(ref _hasSchedule, value);
    }
}
