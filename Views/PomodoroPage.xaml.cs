using System.Runtime.InteropServices;
using Chronvyr.Models;
using Chronvyr.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views;

/// <summary>
/// 番茄钟页（FR-5）。
/// </summary>
public sealed partial class PomodoroPage : Page
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private const uint MbIconAsterisk = 0x00000040;

    public PomodoroPage()
    {
        ViewModel = new PomodoroViewModel(App.Pomodoro);

        InitializeComponent();

        ViewModel.Finished += OnPomodoroFinished;
    }

    /// <summary>页面视图模型。</summary>
    public PomodoroViewModel ViewModel { get; }

    /// <summary>从未完成日程中选择任务（FR-5.1）。</summary>
    private void OnPickScheduleClick(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        foreach (var item in App.Data.Schedules)
        {
            if (item.IsCompleted)
            {
                continue;
            }

            var menuItem = new MenuFlyoutItem
            {
                Text = $"{item.Title}（{item.DueDateText}）",
                Tag = item,
            };

            menuItem.Click += OnScheduleChosen;
            flyout.Items.Add(menuItem);
        }

        if (flyout.Items.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = "暂无未完成日程", IsEnabled = false });
        }

        flyout.ShowAt((FrameworkElement)sender);
    }

    private void OnScheduleChosen(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: ScheduleItem item })
        {
            ViewModel.TaskName = item.Title;
        }
    }

    private void OnPomodoroFinished(object? sender, EventArgs e)
    {
        DoneBar.IsOpen = true;

        // 非打包应用无法直接使用 AppNotification，采用系统提示音提醒。
        try
        {
            _ = MessageBeep(MbIconAsterisk);
        }
        catch (DllNotFoundException)
        {
            // 忽略：本机不支持提示音。
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);
}
