using Chronvyr.Models;
using Chronvyr.ViewModels;
using Chronvyr.Views.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views;

/// <summary>
/// 日程管理页（FR-3）。
/// </summary>
public sealed partial class SchedulePage : Page
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    public SchedulePage()
    {
        ViewModel = new ScheduleViewModel(App.Data.Schedules);

        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>页面视图模型。</summary>
    public ScheduleViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e) => App.Data.Changed += OnDataChanged;

    private void OnUnloaded(object sender, RoutedEventArgs e) => App.Data.Changed -= OnDataChanged;

    private void OnDataChanged(object? sender, EventArgs e) => ViewModel.Refresh();

    private void OnFilterChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            ViewModel.Filter = tag switch
            {
                "pending" => ScheduleFilter.Pending,
                "completed" => ScheduleFilter.Completed,
                _ => ScheduleFilter.All,
            };
        }
    }

    /// <summary>新建日程（FR-3.1）。</summary>
    private async void OnNewScheduleClick(object sender, RoutedEventArgs e)
    {
        var item = new ScheduleItem { DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)) };
        var dialog = new ScheduleEditDialog(item, isNew: true) { XamlRoot = XamlRoot };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Data.Schedules.Add(item);
            ViewModel.Refresh();
        }
    }

    /// <summary>编辑日程（FR-3.2）。取消时不会写入任何改动。</summary>
    private async void OnEditScheduleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduleItem item })
        {
            return;
        }

        var dialog = new ScheduleEditDialog(item, isNew: false) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    /// <summary>删除日程（FR-3.3，需确认）。</summary>
    private async void OnDeleteScheduleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScheduleItem item })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除日程",
            Content = $"确定要删除「{item.Title}」吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Data.Schedules.Remove(item);
            ViewModel.Refresh();
        }
    }
}
