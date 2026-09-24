namespace Chronvyr.Models;

/// <summary>
/// 节假日/调休日（数据源：NateScarlet/holiday-cn，FR-2.4）。
/// </summary>
public sealed class HolidayDay
{
    /// <summary>节日名称，如「春节」「国庆节」。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>true 为法定放假日，false 为调休上班日。</summary>
    public bool IsOffDay { get; set; }
}

/// <summary>holiday-cn 的年度 JSON 结构。</summary>
internal sealed class HolidayFile
{
    public int Year { get; set; }

    public List<HolidayDay> Days { get; set; } = [];
}
