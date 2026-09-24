using Chronvyr.Models;
using Chronvyr.ViewModels;
using Chronvyr.Views.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views;

/// <summary>
/// 工作台页（FR-6）：多项目协同管理。
/// </summary>
public sealed partial class WorkbenchPage : Page
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    public WorkbenchPage()
    {
        ViewModel = new WorkbenchViewModel(App.Data.Projects);

        InitializeComponent();
    }

    /// <summary>页面视图模型。</summary>
    public WorkbenchViewModel ViewModel { get; }

    /// <summary>新建项目（FR-6.2）。</summary>
    private async void OnNewProjectClick(object sender, RoutedEventArgs e)
    {
        var project = new WorkProject();
        var dialog = new ProjectEditDialog(project, isNew: true) { XamlRoot = XamlRoot };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Data.Projects.Add(project);
        }
    }

    /// <summary>编辑项目（FR-6.2 / FR-6.3）。</summary>
    private async void OnEditProjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkProject project })
        {
            return;
        }

        var dialog = new ProjectEditDialog(project, isNew: false) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    /// <summary>删除项目（FR-6.2，需确认）。</summary>
    private async void OnDeleteProjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkProject project })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除项目",
            Content = $"确定要删除「{project.Name}」及其全部更新日志吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Data.Projects.Remove(project);
        }
    }
}
