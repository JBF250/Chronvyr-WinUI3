using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>
/// 日记页视图模型（FR-4）。
/// </summary>
public sealed class DiaryViewModel : ObservableObject
{
    private readonly ObservableCollection<DiaryEntry> _source;
    private DiaryEntry? _selected;
    private DateTimeOffset? _savedAt;

    public DiaryViewModel(ObservableCollection<DiaryEntry> source)
    {
        _source = source;
        Refresh();
    }

    /// <summary>历史日记（按日期倒序）。</summary>
    public ObservableCollection<DiaryEntry> Entries { get; } = [];

    /// <summary>当前编辑的日记；为 null 表示未选中。</summary>
    public DiaryEntry? SelectedEntry
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
            {
                return;
            }

            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(EditingTitle));
            OnPropertyChanged(nameof(EditingContent));
            OnPropertyChanged(nameof(EditingDate));
        }
    }

    /// <summary>是否已选中一篇日记。</summary>
    public bool HasSelection => _selected is not null;

    /// <summary>是否尚未选择（用于空状态提示）。</summary>
    public bool IsNothingSelected => _selected is null;

    /// <summary>是否还没有任何日记。</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>标题（双向绑定到编辑区）。</summary>
    public string EditingTitle
    {
        get => _selected?.Title ?? string.Empty;
        set
        {
            if (_selected is not null && _selected.Title != value)
            {
                _selected.Title = value;
                _selected.UpdatedAt = DateTimeOffset.Now;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>正文（双向绑定到编辑区）。</summary>
    public string EditingContent
    {
        get => _selected?.Content ?? string.Empty;
        set
        {
            if (_selected is not null && _selected.Content != value)
            {
                _selected.Content = value;
                _selected.UpdatedAt = DateTimeOffset.Now;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>日期（双向绑定到编辑区）。</summary>
    public DateTimeOffset EditingDate
    {
        get => _selected?.Date ?? DateTimeOffset.Now;
        set
        {
            if (_selected is not null && _selected.Date != value)
            {
                _selected.Date = value;
                _selected.UpdatedAt = DateTimeOffset.Now;
                OnPropertyChanged();
                Refresh();
            }
        }
    }

    /// <summary>
    /// 按日期倒序重建历史列表。
    /// </summary>
    /// <remarks>
    /// 必须保留选中项：<c>Entries.Clear()</c> 会让 ListView 的 SelectedItem 变成 null，
    /// 而它双向绑定到 SelectedEntry，从而导致编辑区内容被清空
    /// （表现为「改完日期，日记内容就消失了」）。
    /// </remarks>
    public void Refresh()
    {
        var keep = _selected;

        var ordered = _source.OrderByDescending(entry => entry.Date).ThenByDescending(entry => entry.CreatedAt).ToList();

        Entries.Clear();
        foreach (var entry in ordered)
        {
            Entries.Add(entry);
        }

        // Clear() 之后 _selected 可能已被绑定回调置空，这里无条件恢复。
        if (keep is not null)
        {
            SelectedEntry = keep;
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// 把已经编辑好的日记加入集合并选中（FR-4.1）。
    /// 内容由新建对话框填好后再传进来，这里不负责造空条目——
    /// 否则取消对话框会在列表里留下一条半成品。
    /// </summary>
    public void Add(DiaryEntry entry)
    {
        _source.Add(entry);
        Refresh();
        SelectedEntry = entry;
    }

    /// <summary>最近一次手动保存的时间；为空表示还没手动保存过。</summary>
    public DateTimeOffset? SavedAt
    {
        get => _savedAt;
        private set
        {
            if (SetProperty(ref _savedAt, value))
            {
                OnPropertyChanged(nameof(SaveHint));
            }
        }
    }

    /// <summary>保存结果的提示文字（空串表示本次还没手动保存过）。</summary>
    public string SaveHint => _savedAt is { } time
        ? App.Loc.Format("DiarySavedAt", time.ToString("HH:mm:ss"))
        : string.Empty;

    /// <summary>
    /// 保存按钮：把编辑区内容立刻落盘。
    /// </summary>
    /// <remarks>
    /// 平时改一个字段就会自动保存（数据层有 500ms 防抖），这个按钮给的是一个
    /// 明确的确认动作与可见反馈——「到底存没存」不该让使用者去猜。
    /// </remarks>
    public void Save()
    {
        if (_selected is null)
        {
            return;
        }

        _selected.UpdatedAt = DateTimeOffset.Now;
        App.Data.Flush();
        SavedAt = DateTimeOffset.Now;
    }

    /// <summary>删除当前选中的日记（FR-4.2）。</summary>
    public void DeleteSelected()
    {
        if (_selected is null)
        {
            return;
        }

        var target = _selected;
        SelectedEntry = null;
        _source.Remove(target);
        Refresh();
    }
}
