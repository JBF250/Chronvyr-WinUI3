using System.Collections.ObjectModel;
using Chronvyr.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views.Dialogs;

/// <summary>
/// 新建 / 编辑工作台项目对话框（FR-6.2 / FR-6.3）：
/// 项目名称、当前版本号、开发进度、更新日志。
/// 对话框在临时集合上编辑更新日志，只有点击保存才会写回，取消不影响原数据。
/// </summary>
public sealed partial class ProjectEditDialog : ContentDialog
{
    private readonly WorkProject _target;
    private readonly ObservableCollection<UpdateLogEntry> _workingLogs = [];

    public ProjectEditDialog(WorkProject target, bool isNew)
    {
        _target = target;

        InitializeComponent();

        Title = isNew ? "新建项目" : "编辑项目";
        PrimaryButtonText = isNew ? "创建" : "保存";

        NameBox.Text = target.Name;
        VersionBox.Text = target.Version;
        ProgressBox.Value = target.Progress;

        foreach (var log in target.UpdateLogs)
        {
            _workingLogs.Add(new UpdateLogEntry { Date = log.Date, Content = log.Content });
        }

        RebuildLogRows();

        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(target.Name);
        NameBox.TextChanged += (_, _) => IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void RebuildLogRows()
    {
        LogsPanel.Children.Clear();

        if (_workingLogs.Count == 0)
        {
            LogsPanel.Children.Add(new TextBlock
            {
                Text = "还没有更新日志，点击下方按钮添加。",
                Opacity = 0.6,
                FontSize = 12,
            });
            return;
        }

        foreach (var log in _workingLogs)
        {
            LogsPanel.Children.Add(CreateLogRow(log));
        }
    }

    private FrameworkElement CreateLogRow(UpdateLogEntry log)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var datePicker = new DatePicker
        {
            Date = log.Date,
            Width = 132,
        };
        datePicker.DateChanged += (_, args) => log.Date = args.NewDate;

        var contentBox = new TextBox
        {
            Text = log.Content,
            PlaceholderText = "这次更新了什么？",
            TextWrapping = TextWrapping.Wrap,
        };
        contentBox.TextChanged += (_, _) => log.Content = contentBox.Text;

        var deleteButton = new Button
        {
            Padding = new Thickness(8),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new FontIcon { Glyph = "\uE74D", FontSize = 14 },
        };
        deleteButton.Click += (_, _) =>
        {
            _workingLogs.Remove(log);
            RebuildLogRows();
        };

        Grid.SetColumn(datePicker, 0);
        Grid.SetColumn(contentBox, 1);
        Grid.SetColumn(deleteButton, 2);

        grid.Children.Add(datePicker);
        grid.Children.Add(contentBox);
        grid.Children.Add(deleteButton);

        return grid;
    }

    private void OnAddLogClick(object sender, RoutedEventArgs e)
    {
        _workingLogs.Add(new UpdateLogEntry { Date = DateTimeOffset.Now });
        RebuildLogRows();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            args.Cancel = true;
            return;
        }

        _target.Name = name;
        _target.Version = VersionBox.Text.Trim();
        _target.Progress = double.IsNaN(ProgressBox.Value) ? 0 : (int)ProgressBox.Value;

        // 过滤掉空内容的日志后整体替换，触发数据层落盘。
        var cleaned = new ObservableCollection<UpdateLogEntry>(
            _workingLogs
                .Where(log => !string.IsNullOrWhiteSpace(log.Content))
                .OrderByDescending(log => log.Date));

        _target.UpdateLogs = cleaned;
    }
}
