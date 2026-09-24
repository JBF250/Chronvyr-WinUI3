using SkiaSharp;

namespace Chronvyr.Widgets;

/// <summary>
/// 自绘控件的配色。
/// </summary>
/// <remarks>
/// 取值与 <c>Themes/Styles.xaml</c> 里的 <c>Chronvyr*Brush</c> 保持一致，
/// 这样自绘控件与 WinUI 主界面在同一主题下看起来是一套东西。
///
/// 配色规则参考 NotchPeninsula：主文字用纯色，其余元素一律由文字色派生透明度。
/// 副文字不用「百分比白」而用固定值——百分比白叠在实色卡片上会发灰、看久了累。
/// </remarks>
internal sealed class WidgetTheme
{
    private WidgetTheme(bool isLight)
    {
        IsLight = isLight;

        if (isLight)
        {
            // 浅色：卡片用近乎不透明的白，与 XAML 的 #F7FAFAFA 对应。
            Card = new SKColor(0xFA, 0xFA, 0xFA);
            CardHover = new SKColor(0xF0, 0xF0, 0xF0);
            Stroke = new SKColor(0, 0, 0, 0x14);
            TextPrimary = new SKColor(0x1A, 0x1A, 0x1A);
            TextSubtle = new SKColor(0x50, 0x50, 0x50);
            Divider = new SKColor(0, 0, 0, 0x14);
            Accent = new SKColor(0x0A, 0x5B, 0xC4);
            Danger = new SKColor(0xC4, 0x2B, 0x1C);
        }
        else
        {
            // 深色：卡片不透明，避免露出系统绘制的窗口底色。
            Card = new SKColor(0x1E, 0x1E, 0x1E);
            CardHover = new SKColor(0x26, 0x26, 0x26);
            Stroke = new SKColor(255, 255, 255, 0x12);
            TextPrimary = SKColors.White;
            TextSubtle = new SKColor(0xC8, 0xC8, 0xC8);
            Divider = new SKColor(255, 255, 255, 0x14);
            Accent = new SKColor(0x7C, 0xC4, 0xFF);
            Danger = new SKColor(0xE8, 0x6B, 0x5C);
        }

        // 日历节假日标记沿用 XAML 里的红/灰，两种主题下都足够醒目。
        HolidayOff = new SKColor(0xD6, 0x45, 0x45);
        HolidayWork = new SKColor(0x7A, 0x7A, 0x7A);
    }

    /// <summary>深色配色。</summary>
    public static WidgetTheme Dark { get; } = new(isLight: false);

    /// <summary>浅色配色。</summary>
    public static WidgetTheme Light { get; } = new(isLight: true);

    /// <summary>按主题取配色。</summary>
    public static WidgetTheme From(bool isLight) => isLight ? Light : Dark;

    /// <summary>是否为浅色主题。</summary>
    public bool IsLight { get; }

    /// <summary>卡片底色。</summary>
    public SKColor Card { get; }

    /// <summary>卡片悬停色。</summary>
    public SKColor CardHover { get; }

    /// <summary>卡片描边。</summary>
    public SKColor Stroke { get; }

    /// <summary>主文字。</summary>
    public SKColor TextPrimary { get; }

    /// <summary>次要文字。</summary>
    public SKColor TextSubtle { get; }

    /// <summary>分隔线。</summary>
    public SKColor Divider { get; }

    /// <summary>强调色。</summary>
    public SKColor Accent { get; }

    /// <summary>危险色（删除等）。</summary>
    public SKColor Danger { get; }

    /// <summary>法定放假日标记。</summary>
    public SKColor HolidayOff { get; }

    /// <summary>调休上班日标记。</summary>
    public SKColor HolidayWork { get; }

    /// <summary>由主色派生一个更淡的版本（用于悬停底、标签底等）。</summary>
    public static SKColor Fade(SKColor color, byte alpha) => color.WithAlpha(alpha);
}
