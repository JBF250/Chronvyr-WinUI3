using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.ViewModels;

/// <summary>
/// 工作台页视图模型（FR-6）：多项目协同管理。
/// </summary>
public sealed class WorkbenchViewModel : ObservableObject
{
    public WorkbenchViewModel(ObservableCollection<WorkProject> projects)
    {
        Projects = projects;
        Projects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>全部项目。</summary>
    public ObservableCollection<WorkProject> Projects { get; }

    /// <summary>是否还没有项目。</summary>
    public bool IsEmpty => Projects.Count == 0;
}
