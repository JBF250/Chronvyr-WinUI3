using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronvyr.Models;

/// <summary>
/// 日程（FR-3）。字段对应 FR-3.1：标题、描述、开始日期、截止日期。
/// </summary>
/// <remarks>
/// 日期一律只到「天」：使用者要的是「哪几天要做这件事」，精确到分钟只会让填写变麻烦。
/// 跨天日程由「开始日期 + 截止日期」表达，日历上覆盖到的每一天都会标出它。
/// <para>
/// 项目约定：可观察属性统一手写 <c>get =&gt; _field; set =&gt; SetProperty(ref _field, value)</c>。
/// 不使用 CommunityToolkit 的 <c>[ObservableProperty]</c> 源生成器：其字段形式在本 WinUI3 项目下
/// 会触发 MVVMTK0045（WinRT AOT 兼容性）警告，而 partial property 形式在 8.4.2 下不生成实现。
/// </para>
/// </remarks>
public partial class ScheduleItem : ObservableObject
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private DateOnly _dueDate = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly? _startDate;
    private bool _isCompleted;

    /// <summary>唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>标题。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>描述。</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>截止日期。</summary>
    public DateOnly DueDate
    {
        get => _dueDate;
        set
        {
            if (SetProperty(ref _dueDate, value))
            {
                RaiseDerived();
            }
        }
    }

    /// <summary>
    /// 开始日期（可空）。
    /// 为空表示单日日程，整条日程只落在截止日那天；填了就变成跨天日程，
    /// 日历上从开始日到截止日之间的每一天都会标出它。
    /// </summary>
    public DateOnly? StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                RaiseDerived();
            }
        }
    }

    /// <summary>是否已完成（FR-3.4）。</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>实际生效的开始日期：未填开始日期时即截止日。</summary>
    [JsonIgnore]
    public DateOnly EffectiveStartDate =>
        _startDate is { } start && start < _dueDate ? start : _dueDate;

    /// <summary>是否显式填了开始日期。</summary>
    [JsonIgnore]
    public bool HasStartDate => _startDate is not null;

    /// <summary>是否跨越多天。</summary>
    [JsonIgnore]
    public bool SpansMultipleDays => EffectiveStartDate < DueDate;

    /// <summary>跨天区间展示文本，如「09-19 – 09-21」；单日日程返回空串。</summary>
    [JsonIgnore]
    public string SpanText => SpansMultipleDays
        ? $"{EffectiveStartDate:MM-dd} – {DueDate:MM-dd}"
        : string.Empty;

    /// <summary>
    /// 日期展示文本。单日「2026/9/21」，跨天「2026/9/21 – 2026/9/25」。
    /// </summary>
    [JsonIgnore]
    public string RangeText => SpansMultipleDays
        ? $"{EffectiveStartDate:yyyy/M/d} – {DueDate:yyyy/M/d}"
        : DueDate.ToString("yyyy/M/d");

    /// <summary>截止日期的展示文本（列表、下拉等处使用）。</summary>
    [JsonIgnore]
    public string DueDateText => DueDate.ToString("yyyy/M/d");

    /// <summary>这条日程是否覆盖指定日期（跨天日程覆盖的每一天都算）。</summary>
    public bool CoversDate(DateOnly date) => date >= EffectiveStartDate && date <= DueDate;

    /// <summary>
    /// 兼容 1.1 及更早的数据：那一版存的是截止【时间】（DateTimeOffset），
    /// 反序列化时取它的日期部分。读取端永远返回 null，所以不会把旧字段写回文件。
    /// </summary>
    [JsonPropertyName("DueTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LegacyDueTime
    {
        get => null;
        set
        {
            if (value is { } time)
            {
                _dueDate = DateOnly.FromDateTime(time.LocalDateTime);
            }
        }
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(EffectiveStartDate));
        OnPropertyChanged(nameof(HasStartDate));
        OnPropertyChanged(nameof(SpansMultipleDays));
        OnPropertyChanged(nameof(SpanText));
        OnPropertyChanged(nameof(RangeText));
        OnPropertyChanged(nameof(DueDateText));
    }
}
