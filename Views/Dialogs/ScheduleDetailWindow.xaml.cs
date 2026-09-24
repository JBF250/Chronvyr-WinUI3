using Chronvyr.Models;
using Chronvyr.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Chronvyr.Views.Dialogs;

/// <summary>
/// 独立的日程详情窗口（FR-5.3）。
/// 桌面日程表里新建的条目会直接开在这里，且所有字段都可编辑，改动即时落盘。
/// </summary>
public sealed partial class ScheduleDetailWindow : Window
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private const int WindowWidth = 560;
    private const int WindowHeight = 520;

    private static readonly List<ScheduleDetailWindow> OpenWindows = [];

    private readonly ScheduleItem _item;
    private bool _loading = true;

    public ScheduleDetailWindow(ScheduleItem item)
    {
        _item = item;

        InitializeComponent();

        Helpers.WindowHelper.ApplyAppIcon(this);
        Title = string.IsNullOrWhiteSpace(item.Title) ? "日程详情" : item.Title;

        var start = item.EffectiveStartDate;

        TitleBox.Text = item.Title;
        DescBox.Text = item.Description;
        StartDatePicker.Date = start.ToDateTime(TimeOnly.MinValue);
        DueDatePicker.Date = item.DueDate.ToDateTime(TimeOnly.MinValue);
        CompletedBox.IsChecked = item.IsCompleted;

        var isLight = SettingsService.Resolve(App.Settings.Current.MainStyle) == VisualStyle.Light;
        SystemBackdrop = ThemeService.CreateBackdrop(App.Settings.Current.MainStyle);
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        ThemeService.ApplyWindowChrome(this, isLight);

        ResizeAndCenter();

        _loading = false;
        Closed += OnClosed;
    }

    /// <summary>打开（或激活已打开的）日程详情窗口。</summary>
    public static void Open(ScheduleItem item)
    {
        foreach (var existing in OpenWindows)
        {
            if (ReferenceEquals(existing._item, item))
            {
                Helpers.WindowHelper.Show(existing);
                return;
            }
        }

        var window = new ScheduleDetailWindow(item);
        OpenWindows.Add(window);
        Helpers.WindowHelper.Show(window);
    }

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _item.Title = TitleBox.Text;
        Title = string.IsNullOrWhiteSpace(_item.Title) ? "日程详情" : _item.Title;
    }

    private void OnDescChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _item.Description = DescBox.Text;
    }

    private void OnStartDateChanged(object sender, DatePickerValueChangedEventArgs e) => ApplyDates();

    private void OnDueDateChanged(object sender, DatePickerValueChangedEventArgs e) => ApplyDates();

    /// <summary>
    /// 日期变更。截止被拉到开始之前时，把两者对调成一个合法区间——
    /// 顺序填反是最常见的笔误，直接纠正比弹一句「日期不对」更有用。
    /// </summary>
    private void ApplyDates()
    {
        if (_loading)
        {
            return;
        }

        var start = DateOnly.FromDateTime(StartDatePicker.Date.Date);
        var due = DateOnly.FromDateTime(DueDatePicker.Date.Date);

        if (due < start)
        {
            (start, due) = (due, start);

            _loading = true;
            StartDatePicker.Date = start.ToDateTime(TimeOnly.MinValue);
            DueDatePicker.Date = due.ToDateTime(TimeOnly.MinValue);
            _loading = false;
        }

        _item.DueDate = due;

        // 起止同一天就是单日日程，不必记一个多余的开始日期。
        _item.StartDate = start == due ? null : start;
    }

    private void OnCompletedChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            _item.IsCompleted = CompletedBox.IsChecked == true;
        }
    }

    private void ResizeAndCenter()
    {
        AppWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (area is null)
        {
            return;
        }

        var work = area.WorkArea;
        var x = work.X + ((work.Width - WindowWidth) / 2);
        var y = work.Y + ((work.Height - WindowHeight) / 2);
        AppWindow.Move(new PointInt32(x, y));
    }

    private void OnClosed(object sender, WindowEventArgs args) => OpenWindows.Remove(this);
}
