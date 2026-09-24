using System.Globalization;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronvyr.Models;

/// <summary>
/// 日记（FR-4）。桌面日记栏在预览态只显示标题（FR-6.5.2），因此标题为必备字段。
/// </summary>
public partial class DiaryEntry : ObservableObject
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private DateTimeOffset _date = DateTimeOffset.Now;

    /// <summary>唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>标题。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>正文内容。</summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>所属日期（用于按日期查看历史日记 FR-4.3）。</summary>
    public DateTimeOffset Date
    {
        get => _date;
        set
        {
            if (SetProperty(ref _date, value))
            {
                OnPropertyChanged(nameof(DateText));
            }
        }
    }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>最后修改时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>内容摘要，用于列表与桌面日记栏预览。</summary>
    [JsonIgnore]
    public string Preview => Content.Length <= 80 ? Content : Content[..80] + "…";

    /// <summary>日期展示文本，如「2026年9月19日」。</summary>
    [JsonIgnore]
    public string DateText => Date.LocalDateTime.ToString("d");
}
