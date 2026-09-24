using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>日程列表筛选方式。</summary>
public enum ScheduleFilter
{
    /// <summary>全部。</summary>
    All,

    /// <summary>仅未完成。</summary>
    Pending,

    /// <summary>仅已完成。</summary>
    Completed,
}

/// <summary>
/// 日程管理页视图模型（FR-3）。
/// </summary>
public sealed class ScheduleViewModel : ObservableObject
{
    private readonly ObservableCollection<ScheduleItem> _source;
    private ScheduleFilter _filter = ScheduleFilter.All;

    public ScheduleViewModel(ObservableCollection<ScheduleItem> source)
    {
        _source = source;
        Refresh();
    }

    /// <summary>按当前筛选与排序后的日程。</summary>
    public ObservableCollection<ScheduleItem> Items { get; } = [];

    /// <summary>当前筛选方式。</summary>
    public ScheduleFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                Refresh();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterPending));
                OnPropertyChanged(nameof(IsFilterCompleted));
            }
        }
    }

    public bool IsFilterAll => _filter == ScheduleFilter.All;

    public bool IsFilterPending => _filter == ScheduleFilter.Pending;

    public bool IsFilterCompleted => _filter == ScheduleFilter.Completed;

    /// <summary>列表是否为空（用于显示空状态提示）。</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>统计文本，如「共 8 项 · 未完成 3 项」。</summary>
    public string SummaryText
    {
        get
        {
            var total = _source.Count;
            var pending = 0;
            foreach (var item in _source)
            {
                if (!item.IsCompleted)
                {
                    pending++;
                }
            }

            return $"共 {total} 项 · 未完成 {pending} 项";
        }
    }

    /// <summary>按当前筛选条件重建列表。</summary>
    public void Refresh()
    {
        var query = _source.Where(item => _filter switch
        {
            ScheduleFilter.Pending => !item.IsCompleted,
            ScheduleFilter.Completed => item.IsCompleted,
            _ => true,
        });

        // 先按截止日期升序；已完成项整体后置。
        var ordered = query
            .OrderBy(item => item.IsCompleted)
            .ThenBy(item => item.DueDate)
            .ToList();

        Items.Clear();
        foreach (var item in ordered)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }
}
