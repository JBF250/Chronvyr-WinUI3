using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronvyr.Models;

/// <summary>
/// 工作台项目（FR-6）。含版本号、开发进度、更新日志。
/// </summary>
public partial class WorkProject : ObservableObject
{
    private ObservableCollection<UpdateLogEntry> _logs = [];
    private string _name = string.Empty;
    private string _version = "1.0.0";
    private int _progress;

    /// <summary>唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>项目名称。</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>当前版本号。</summary>
    public string Version
    {
        get => _version;
        set
        {
            if (SetProperty(ref _version, value))
            {
                OnPropertyChanged(nameof(VersionText));
            }
        }
    }

    /// <summary>开发进度百分比（0-100），以环形进度条呈现。</summary>
    public int Progress
    {
        get => _progress;
        set
        {
            if (SetProperty(ref _progress, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    /// <summary>
    /// 更新日志。内部条目的增删改都会冒泡为本对象的属性变更，
    /// 以便数据层据此实时落盘（NFR-4.1）。
    /// </summary>
    public ObservableCollection<UpdateLogEntry> UpdateLogs
    {
        get => _logs;
        set
        {
            var next = value ?? [];
            if (ReferenceEquals(_logs, next))
            {
                return;
            }

            HookLogs(_logs, attach: false);
            _logs = next;
            HookLogs(_logs, attach: true);

            // 必须先通知 UpdateLogs 让绑定换源，再通知派生属性。
            // 之前漏了 LatestLogText，导致「编辑对话框里填了日志、卡片却仍显示暂无更新日志」——
            // 因为保存时是整体替换集合，走的是这个 setter，而集合内部的增删才会调用 NotifyLogDerived。
            OnPropertyChanged();
            OnPropertyChanged(nameof(LatestLogText));
        }
    }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>版本号展示文本。</summary>
    [JsonIgnore]
    public string VersionText => string.IsNullOrWhiteSpace(Version) ? "未设置版本号" : $"v{Version}";

    /// <summary>进度百分比展示文本。</summary>
    [JsonIgnore]
    public string ProgressText => $"{Progress}%";

    /// <summary>最近一条更新日志内容。</summary>
    [JsonIgnore]
    public string LatestLogText => _logs.Count == 0
        ? "暂无更新日志"
        : _logs.OrderByDescending(log => log.Date).First().Content;

    public WorkProject()
    {
        HookLogs(_logs, attach: true);
    }

    private void NotifyLogDerived()
    {
        OnPropertyChanged(nameof(UpdateLogs));
        OnPropertyChanged(nameof(LatestLogText));
    }

    private void HookLogs(ObservableCollection<UpdateLogEntry> logs, bool attach)
    {
        if (attach)
        {
            logs.CollectionChanged += OnLogsCollectionChanged;
        }
        else
        {
            logs.CollectionChanged -= OnLogsCollectionChanged;
        }

        foreach (var entry in logs)
        {
            if (attach)
            {
                entry.PropertyChanged += OnLogEntryChanged;
            }
            else
            {
                entry.PropertyChanged -= OnLogEntryChanged;
            }
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (UpdateLogEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnLogEntryChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (UpdateLogEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnLogEntryChanged;
            }
        }

        OnPropertyChanged(nameof(UpdateLogs));
        OnPropertyChanged(nameof(LatestLogText));
    }

    private void OnLogEntryChanged(object? sender, PropertyChangedEventArgs e) =>
        NotifyLogDerived();
}

/// <summary>项目更新日志条目。</summary>
public partial class UpdateLogEntry : ObservableObject
{
    private DateTimeOffset _date = DateTimeOffset.Now;
    private string _content = string.Empty;

    /// <summary>唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>日志日期。</summary>
    public DateTimeOffset Date
    {
        get => _date;
        set => SetProperty(ref _date, value);
    }

    /// <summary>日志内容。</summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
}
