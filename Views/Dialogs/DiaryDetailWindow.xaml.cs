using Chronvyr.Models;
using Chronvyr.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Chronvyr.Views.Dialogs;

/// <summary>
/// 独立的日记详情窗口（FR-5.2）。
/// 桌面日记栏里新建的日记会直接开在这里，且标题 / 日期 / 正文都可编辑，改动即时落盘。
/// </summary>
public sealed partial class DiaryDetailWindow : Window
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private const int WindowWidth = 600;
    private const int WindowHeight = 640;

    private static readonly List<DiaryDetailWindow> OpenWindows = [];

    private readonly DiaryEntry _entry;
    private bool _loading = true;

    public DiaryDetailWindow(DiaryEntry entry)
    {
        _entry = entry;

        InitializeComponent();

        Helpers.WindowHelper.ApplyAppIcon(this);
        Title = string.IsNullOrWhiteSpace(entry.Title) ? "日记详情" : entry.Title;

        TitleBox.Text = entry.Title;
        DatePickerBox.Date = entry.Date;
        ContentBox.Text = entry.Content;

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

    /// <summary>打开（或激活已打开的）日记详情窗口。</summary>
    public static void Open(DiaryEntry entry)
    {
        foreach (var existing in OpenWindows)
        {
            if (ReferenceEquals(existing._entry, entry))
            {
                Helpers.WindowHelper.Show(existing);
                return;
            }
        }

        var window = new DiaryDetailWindow(entry);
        OpenWindows.Add(window);
        Helpers.WindowHelper.Show(window);
    }

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _entry.Title = TitleBox.Text;
        _entry.UpdatedAt = DateTimeOffset.Now;
        Title = string.IsNullOrWhiteSpace(_entry.Title) ? "日记详情" : _entry.Title;
    }

    private void OnContentChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _entry.Content = ContentBox.Text;
        _entry.UpdatedAt = DateTimeOffset.Now;
    }

    private void OnDateChanged(object sender, DatePickerValueChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _entry.Date = DatePickerBox.Date;
        _entry.UpdatedAt = DateTimeOffset.Now;
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
