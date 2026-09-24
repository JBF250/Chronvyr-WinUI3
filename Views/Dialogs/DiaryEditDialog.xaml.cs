using Chronvyr.Models;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views.Dialogs;

/// <summary>
/// 新建日记对话框（FR-4.1）：标题、日期、内容一次填完。
/// </summary>
/// <remarks>
/// 与日程的新建对话框同一套路：内容先写进一个**尚未入库**的 DiaryEntry，
/// 点「创建」才由调用方写入集合，点「取消」整条丢弃，不会在列表里留半成品。
/// </remarks>
public sealed partial class DiaryEditDialog : ContentDialog
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private readonly DiaryEntry _target;

    public DiaryEditDialog(DiaryEntry target, bool isNew)
    {
        _target = target;

        InitializeComponent();

        Title = isNew ? App.Loc["DiaryNew"] : App.Loc["DiaryDetailTitle"];
        PrimaryButtonText = isNew ? App.Loc["ActionCreate"] : App.Loc["ActionSave"];

        TitleBox.Text = target.Title;
        DateBox.Date = target.Date;
        ContentBox.Text = target.Content;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var title = TitleBox.Text.Trim();

        _target.Title = string.IsNullOrEmpty(title) ? App.Loc["DiaryUntitled"] : title;
        _target.Date = DateBox.Date;
        _target.Content = ContentBox.Text;
        _target.UpdatedAt = DateTimeOffset.Now;
    }
}
