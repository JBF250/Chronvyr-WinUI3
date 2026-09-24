using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>
/// 桌面日程表视图模型（FR-5.3）：显示全部日程，预览态只显示标题。
/// </summary>
public sealed class ScheduleBoardViewModel : ObservableObject
{
    private readonly ObservableCollection<ScheduleItem> _source;

    public ScheduleBoardViewModel(ObservableCollection<ScheduleItem> source)
    {
        _source = source;
        Refresh();
    }

    /// <summary>全部日程（未完成在前，按截止时间升序）。</summary>
    public ObservableCollection<ScheduleItem> Entries { get; } = [];

    /// <summary>是否还没有日程。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>重建列表。</summary>
    public void Refresh()
    {
        var ordered = _source
            .OrderBy(item => item.IsCompleted)
            .ThenBy(item => item.DueDate)
            .ToList();

        Entries.Clear();
        foreach (var item in ordered)
        {
            Entries.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
