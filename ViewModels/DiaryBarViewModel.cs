using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>
/// 桌面日记栏视图模型（FR-5.2）：显示全部日记，预览态只显示标题。
/// </summary>
public sealed class DiaryBarViewModel : ObservableObject
{
    private readonly ObservableCollection<DiaryEntry> _source;

    public DiaryBarViewModel(ObservableCollection<DiaryEntry> source)
    {
        _source = source;
        Refresh();
    }

    /// <summary>全部日记（按日期倒序）。</summary>
    public ObservableCollection<DiaryEntry> Entries { get; } = [];

    /// <summary>是否还没有日记。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>重建列表。</summary>
    public void Refresh()
    {
        var ordered = _source
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.CreatedAt)
            .ToList();

        Entries.Clear();
        foreach (var entry in ordered)
        {
            Entries.Add(entry);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
