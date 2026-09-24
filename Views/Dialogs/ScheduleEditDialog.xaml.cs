using Chronvyr.Models;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views.Dialogs;

/// <summary>
/// 新建 / 编辑日程对话框（FR-3.1 / FR-3.2）：标题、描述、开始日期、截止日期。
/// </summary>
/// <remarks>
/// 调用方约定：新建时先把内容填进一个**尚未入库**的 ScheduleItem，只有点了「创建」
/// 才由调用方写入集合，点「取消」则整条丢弃（见 SchedulePage.OnNewScheduleClick）。
/// </remarks>
public sealed partial class ScheduleEditDialog : ContentDialog
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private readonly ScheduleItem _target;

    public ScheduleEditDialog(ScheduleItem target, bool isNew)
    {
        _target = target;

        InitializeComponent();

        Title = isNew ? App.Loc["ScheduleEditTitle"] : App.Loc["ScheduleEditTitleEdit"];
        PrimaryButtonText = isNew ? App.Loc["ActionCreate"] : App.Loc["ActionSave"];

        TitleBox.Text = target.Title;
        DescBox.Text = target.Description;
        StartDatePicker.Date = target.EffectiveStartDate.ToDateTime(TimeOnly.MinValue);
        DueDatePicker.Date = target.DueDate.ToDateTime(TimeOnly.MinValue);

        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(target.Title);
        TitleBox.TextChanged += (_, _) => IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text);
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            args.Cancel = true;
            return;
        }

        var start = DateOnly.FromDateTime(StartDatePicker.Date.Date);
        var due = DateOnly.FromDateTime(DueDatePicker.Date.Date);

        // 截止早于开始就把两者对调：顺序填反是最常见的笔误，直接存一个合法区间更有用。
        if (due < start)
        {
            (start, due) = (due, start);
        }

        _target.Title = title;
        _target.Description = DescBox.Text.Trim();
        _target.DueDate = due;

        // 起止同一天就是单日日程，不必记一个多余的开始日期。
        _target.StartDate = start == due ? null : start;
    }
}
