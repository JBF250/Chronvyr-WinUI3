using SkiaSharp;

namespace Chronvyr.Widgets;

/// <summary>
/// 控件自绘的视觉原语：圆角、阴影、文本。
/// </summary>
/// <remarks>
/// 这些原先由 XAML / DWM 代劳，但两者都有硬限制：
/// <list type="bullet">
///   <item>XAML 拿不到窗口投影，而 DWM 的窗口圆角只有 8px / 4px 两档；</item>
///   <item>用 SetWindowRgn 硬裁剪能让圆角匹配，但边缘是 1 位图、带明显阶梯锯齿（实测放大 10 倍可见）。</item>
/// </list>
/// 自绘之后两者都能做到抗锯齿，而且阴影不再需要额外窗口。
/// </remarks>
internal static class WidgetPainting
{
    /// <summary>
    /// 生成圆角矩形路径。
    /// </summary>
    /// <remarks>
    /// 用 <see cref="SKPath.ConicTo"/> 配权重 √2/2 而不是 <c>AddRoundRect</c>：
    /// 前者生成的是真正的圆弧（二次有理贝塞尔在权重 0.7071 时恰好等价于 90° 圆弧），
    /// 在放大观察时弧线更均匀，不会出现椭圆化。
    /// </remarks>
    public static SKPath RoundedRect(SKRect rect, float radius)
    {
        var path = new SKPath();
        var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
        const float w = 0.70710678f; // √2 / 2

        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        path.MoveTo(left + r, top);
        path.LineTo(right - r, top);
        path.ConicTo(right, top, right, top + r, w);
        path.LineTo(right, bottom - r);
        path.ConicTo(right, bottom, right - r, bottom, w);
        path.LineTo(left + r, bottom);
        path.ConicTo(left, bottom, left, bottom - r, w);
        path.LineTo(left, top + r);
        path.ConicTo(left, top, left + r, top, w);
        path.Close();

        return path;
    }

    /// <summary>
    /// 绘制柔和投影。
    /// </summary>
    /// <remarks>
    /// 用「多层半透明圆角矩形叠加」模拟高斯模糊，而不是真实模糊滤镜：
    /// 叠层在透明窗口上更快，而且不会因为 CPU 光栅化在边缘产生色带。
    /// 每层半径递增、透明度递减，累积出从中心向外衰减的效果。
    /// </remarks>
    public static void DrawShadow(
        SKCanvas canvas,
        SKRect rect,
        float radius,
        float blur = 18f,
        float alpha = 0.45f,
        float offsetY = 4f,
        int layers = 10)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.Black,
        };

        for (var i = 0; i < layers; i++)
        {
            var progress = (float)i / layers;
            var spread = blur * progress;
            var layerAlpha = alpha * (1f - progress) / layers * 3f;

            paint.Color = new SKColor(0, 0, 0, (byte)Math.Clamp(layerAlpha * 255f, 0, 255));

            var layerRect = new SKRect(
                rect.Left - spread,
                rect.Top - spread + offsetY,
                rect.Right + spread,
                rect.Bottom + spread + offsetY);

            using var path = RoundedRect(layerRect, radius + spread);
            canvas.DrawPath(path, paint);
        }
    }

    /// <summary>填充圆角卡片。</summary>
    public static void FillCard(SKCanvas canvas, SKRect rect, float radius, SKColor fill)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = fill,
        };

        using var path = RoundedRect(rect, radius);
        canvas.DrawPath(path, paint);
    }

    /// <summary>描边圆角卡片。</summary>
    public static void StrokeCard(SKCanvas canvas, SKRect rect, float radius, SKColor stroke, float width = 1f)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            Color = stroke,
        };

        using var path = RoundedRect(rect, radius);
        canvas.DrawPath(path, paint);
    }

    /// <summary>绘制文本，返回实际占用的宽度。</summary>
    public static float DrawText(
        SKCanvas canvas,
        string text,
        float x,
        float y,
        SKColor color,
        float size = 13f,
        bool bold = false,
        SKTextAlign align = SKTextAlign.Left,
        float maxWidth = 0f)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            TextSize = size,
            TextAlign = align,
            Typeface = SKTypeface.FromFamilyName(
                "Microsoft YaHei UI",
                bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
        };

        if (maxWidth > 0)
        {
            paint.TextSize = size;
            var measured = paint.MeasureText(text);
            if (measured > maxWidth)
            {
                // 超出可用宽度时截断并加省略号，避免文字压出卡片。
                var keep = text.Length;
                while (keep > 1 && paint.MeasureText(text[..keep] + "…") > maxWidth)
                {
                    keep--;
                }

                text = text[..keep] + "…";
            }
        }

        canvas.DrawText(text, x, y, paint);
        return paint.MeasureText(text);
    }

    /// <summary>测量文本宽度（不绘制）。</summary>
    public static float MeasureText(string text, float size = 13f, bool bold = false)
    {
        using var paint = new SKPaint
        {
            TextSize = size,
            Typeface = SKTypeface.FromFamilyName(
                "Microsoft YaHei UI",
                bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
        };

        return paint.MeasureText(text);
    }

    /// <summary>填充圆；颜色完全透明时跳过绘制（省掉无谓的合成开销）。</summary>
    private static SKTypeface? _iconTypeface;

    private static SKTypeface IconTypeface
        => _iconTypeface ??= SKTypeface.FromFamilyName("Segoe MDL2 Assets");

    /// <summary>
    /// 画一个 Segoe MDL2 Assets 字形（胶囊栏里的日历、计时器、电池等图标）。
    /// 字体对象缓存复用，避免每帧重新创建。
    /// </summary>
    public static void DrawGlyph(
        SKCanvas canvas,
        string glyph,
        float x,
        float baselineY,
        SKColor color,
        float size = 12f,
        SKTextAlign align = SKTextAlign.Left)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            TextSize = size,
            TextAlign = align,
            Typeface = IconTypeface,
        };

        canvas.DrawText(glyph, x, baselineY, paint);
    }

    /// <summary>量一个图标字形的宽度。</summary>
    public static float MeasureGlyph(string glyph, float size = 12f)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return 0f;
        }

        using var paint = new SKPaint { TextSize = size, Typeface = IconTypeface };
        return paint.MeasureText(glyph);
    }

    public static void FillCircleIf(SKCanvas canvas, SKPoint center, float radius, SKColor color)
    {
        if (color.Alpha == 0 || radius <= 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color,
        };

        canvas.DrawCircle(center, radius, paint);
    }

    /// <summary>绘制加号（新建按钮）。</summary>
    public static void DrawPlus(SKCanvas canvas, SKPoint center, SKColor color, float arm = 5f)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            StrokeCap = SKStrokeCap.Round,
            Color = color,
        };

        canvas.DrawLine(center.X - arm, center.Y, center.X + arm, center.Y, paint);
        canvas.DrawLine(center.X, center.Y - arm, center.X, center.Y + arm, paint);
    }

    /// <summary>绘制直线（分隔线、删除线）。</summary>
    public static void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, float width = 1f)
    {
        if (color.Alpha == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            Color = color,
        };

        canvas.DrawLine(x1, y1, x2, y2, paint);
    }
}
