using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Chronvyr.Services;

/// <summary>
/// 数据仓储：日程、日记、工作台项目。任何变更都会在防抖后实时落盘（NFR-4.1）。
/// 集合与集合内元素的属性变更都会被追踪。
/// </summary>
public sealed class DataService
{
    private const int SaveDebounceMs = 500;

    private readonly DispatcherQueueTimer? _saveTimer;
    private bool _schedulesDirty;
    private bool _diariesDirty;
    private bool _projectsDirty;

    /// <summary>全部日程。</summary>
    public ObservableCollection<Models.ScheduleItem> Schedules { get; } = [];

    /// <summary>全部日记。</summary>
    public ObservableCollection<Models.DiaryEntry> Diaries { get; } = [];

    /// <summary>全部工作台项目。</summary>
    public ObservableCollection<Models.WorkProject> Projects { get; } = [];

    /// <summary>任意数据发生变更时触发（供各页面刷新视图）。</summary>
    public event EventHandler? Changed;

    public DataService()
    {
        AppPaths.EnsureCreated();
        LoadAll();

        _ = new CollectionWatcher<Models.ScheduleItem>(Schedules, () => MarkDirty(DataKind.Schedules));
        _ = new CollectionWatcher<Models.DiaryEntry>(Diaries, () => MarkDirty(DataKind.Diaries));
        _ = new CollectionWatcher<Models.WorkProject>(Projects, () => MarkDirty(DataKind.Projects));

        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue is not null)
        {
            _saveTimer = queue.CreateTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(SaveDebounceMs);
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += (_, _) => Flush();
        }
    }

    /// <summary>从磁盘载入全部数据。</summary>
    public void LoadAll()
    {
        var schedules = JsonStore.Load<List<Models.ScheduleItem>>(AppPaths.SchedulesFile);
        var diaries = JsonStore.Load<List<Models.DiaryEntry>>(AppPaths.DiariesFile);
        var projects = JsonStore.Load<List<Models.WorkProject>>(AppPaths.ProjectsFile);

        Replace(Schedules, schedules);
        Replace(Diaries, diaries);
        Replace(Projects, projects);
    }

    /// <summary>立即把脏数据写入磁盘。</summary>
    public void Flush()
    {
        if (_schedulesDirty)
        {
            _schedulesDirty = false;
            JsonStore.Save(AppPaths.SchedulesFile, Schedules.ToList());
        }

        if (_diariesDirty)
        {
            _diariesDirty = false;
            JsonStore.Save(AppPaths.DiariesFile, Diaries.ToList());
        }

        if (_projectsDirty)
        {
            _projectsDirty = false;
            JsonStore.Save(AppPaths.ProjectsFile, Projects.ToList());
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, List<T>? source)
    {
        target.Clear();
        if (source is null)
        {
            return;
        }

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void MarkDirty(DataKind kind)
    {
        switch (kind)
        {
            case DataKind.Schedules:
                _schedulesDirty = true;
                break;
            case DataKind.Diaries:
                _diariesDirty = true;
                break;
            default:
                _projectsDirty = true;
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);

        if (_saveTimer is null)
        {
            Flush();
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private enum DataKind
    {
        Schedules,
        Diaries,
        Projects,
    }
}

/// <summary>
/// 追踪一个 <see cref="ObservableCollection{T}"/> 的增删改（含元素属性变更），
/// 任意变更时触发回调。
/// </summary>
internal sealed class CollectionWatcher<T>
    where T : INotifyPropertyChanged
{
    private readonly ObservableCollection<T> _collection;
    private readonly Action _onChanged;

    public CollectionWatcher(ObservableCollection<T> collection, Action onChanged)
    {
        _collection = collection;
        _onChanged = onChanged;
        _collection.CollectionChanged += OnCollectionChanged;

        foreach (var item in _collection)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (T item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (T item in e.NewItems)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _onChanged();
            return;
        }

        _onChanged();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => _onChanged();
}
