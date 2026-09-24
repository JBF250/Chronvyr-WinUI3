using Chronvyr.Services;
using Chronvyr.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Chronvyr.Views;

/// <summary>
/// 日历页（FR-2）。
/// </summary>
public sealed partial class CalendarPage : Page
{
    /// <summary>星期表头，随界面语言变化。</summary>
    private static string[] WeekDayNames => App.Loc.Language switch
    {
        Models.AppLanguage.English => ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
        Models.AppLanguage.Japanese => ["月", "火", "水", "木", "金", "土", "日"],
        _ => ["一", "二", "三", "四", "五", "六", "日"],
    };

    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    public CalendarPage()
    {
        ViewModel = new CalendarViewModel(HolidayService.Shared, App.Data.Schedules);

        InitializeComponent();

        ViewModel.MonthRebuilt += OnMonthRebuilt;

        BuildWeekHeader();
        BuildDayCells();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>页面视图模型。</summary>
    public CalendarViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Data.Changed += OnDataChanged;
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => App.Data.Changed -= OnDataChanged;

    private void OnDataChanged(object? sender, EventArgs e) => ViewModel.RefreshScheduleMarks();

    private void OnMonthRebuilt(object? sender, EventArgs e) => BuildDayCells();

    private void BuildWeekHeader()
    {
        WeekHeaderGrid.Children.Clear();
        WeekHeaderGrid.ColumnDefinitions.Clear();

        for (var i = 0; i < 7; i++)
        {
            WeekHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var text = new TextBlock
            {
                Text = WeekDayNames[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.65,
                FontSize = 12,
            };

            Grid.SetColumn(text, i);
            WeekHeaderGrid.Children.Add(text);
        }
    }

    private void BuildDayCells()
    {
        DaysGrid.Children.Clear();
        DaysGrid.ColumnDefinitions.Clear();
        DaysGrid.RowDefinitions.Clear();

        for (var c = 0; c < 7; c++)
        {
            DaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < 6; r++)
        {
            DaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        var template = (DataTemplate)Resources["DayCellTemplate"];
        var style = (Style)Resources["DayCellStyle"];

        for (var i = 0; i < ViewModel.Days.Count && i < 42; i++)
        {
            var cell = new ContentControl
            {
                Style = style,
                ContentTemplate = template,
                Content = ViewModel.Days[i],
            };

            cell.Tapped += OnDayCellTapped;

            Grid.SetRow(cell, i / 7);
            Grid.SetColumn(cell, i % 7);
            DaysGrid.Children.Add(cell);
        }
    }

    private void OnDayCellTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is ContentControl { Content: CalendarDayViewModel day })
        {
            ViewModel.SelectDay(day);
        }
    }
}
