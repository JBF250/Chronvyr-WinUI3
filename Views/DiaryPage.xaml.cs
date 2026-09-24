using Chronvyr.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views;

/// <summary>
/// 日记页（FR-4）。
/// </summary>
public sealed partial class DiaryPage : Page
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    public DiaryPage()
    {
        ViewModel = new DiaryViewModel(App.Data.Diaries);

        InitializeComponent();
    }

    /// <summary>页面视图模型。</summary>
    public DiaryViewModel ViewModel { get; }

    /// <summary>新建日记（FR-4.1）：先弹对话框，点「创建」才落库，取消则完全不留痕。</summary>
    private async void OnNewDiaryClick(object sender, RoutedEventArgs e)
    {
        var entry = new Models.DiaryEntry { Date = DateTimeOffset.Now };
        var dialog = new Dialogs.DiaryEditDialog(entry, isNew: true) { XamlRoot = XamlRoot };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.Add(entry);
        }
    }

    /// <summary>保存当前日记。平时自动保存，这个按钮给一个明确的确认动作与反馈。</summary>
    private void OnSaveDiaryClick(object sender, RoutedEventArgs e) => ViewModel.Save();

    /// <summary>删除日记（FR-4.2，需确认）。</summary>
    private async void OnDeleteDiaryClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is not { } entry)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除日记",
            Content = $"确定要删除「{entry.Title}」吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.DeleteSelected();
        }
    }
}
