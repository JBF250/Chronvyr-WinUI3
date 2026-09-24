using Chronvyr.Models;
using Chronvyr.Services;
using Chronvyr.ViewModels;
using SkiaSharp;

namespace Chronvyr.Widgets;

/// <summary>
/// 桌面胶囊栏控件（自绘版，FR-5.4）。
/// 平时是一条窄条：左＝今日待办、中＝天气（番茄钟运行中改显倒计时）、右＝时间；
/// 点击窄条向下滑出面板，面板里依次是日程 / 日记 / 天气 / 番茄钟 / 工作台 / 硬件 / 剪切板。
/// </summary>
/// <remarks>
/// 窗口尺寸固定在「完全展开」的大小，收起时只是把卡片画矮、其余像素留空。
/// 分层窗口的空白像素对鼠标是穿透的，命中范围另有 <see cref="HitTest"/> 精确控制，
/// 于是展开 / 收起不必反复重建 DIB，动画可以只做绘制层的插值。
/// </remarks>
public sealed class CapsuleBarWidget : WidgetWindow
{
    // ---- 卡片尺寸（逻辑像素）----
    /// <summary>默认卡片宽度。收起与展开共用同一个宽度，由设置页的滑块决定。</summary>
    public const float DefaultWidth = 360f;

    /// <summary>宽度可调范围；设置页的滑块与这里共用同一组边界。</summary>
    public const float MinWidth = 240f;
    public const float MaxWidth = 560f;

    public const float BarHeight = 44f;
    public const float PanelHeight = 640f;
    private const float Radius = 18f;
    private const float ShadowMargin = 16f;

    // ---- 面板内部 ----
    private const float PanelPad = 12f;
    private const float CardRadius = 10f;
    private const float CardPad = 12f;
    private const float CardGap = 10f;
    private const float TitleHeight = 20f;
    private const float InnerGap = 6f;
    private const float ButtonHeight = 30f;

    private const float ScheduleRow = 36f;
    private const float DiaryRow = 32f;
    private const float ProjectRow = 54f;
    private const float HardwareRow = 20f;
    private const float ClipboardRow = 24f;

    private const float WheelStep = 48f;

    /// <summary>与屏幕顶部边缘的默认空隙（逻辑像素）。</summary>
    private const float DefaultTopGap = 8f;

    private enum CardKind
    {
        Schedule,
        Diary,
        Weather,
        Pomodoro,
        Workbench,
        Hardware,
        Clipboard,
    }

    private static readonly CardKind[] CardOrder =
    [
        CardKind.Schedule,
        CardKind.Diary,
        CardKind.Weather,
        CardKind.Pomodoro,
        CardKind.Workbench,
        CardKind.Hardware,
        CardKind.Clipboard,
    ];

    private readonly DynamicIslandViewModel _vm;

    private WidgetTheme _theme = WidgetTheme.Dark;

    /// <summary>展开进度 0（窄条）… 1（完全展开），由弹簧动画驱动。</summary>
    private float _expand;
    private float _animFrom;
    private float _animTo;
    private double _animElapsed;

    private float _scrollOffset;

    private bool _hoverBar;
    private bool _hoverPrimary;
    private bool _hoverExit;
    private int _hoverSchedule = -1;
    private int _hoverClipboard = -1;

    private bool _pressed;

    /// <summary>卡片宽度（逻辑像素），由设置页的滑块决定。</summary>
    private float _cardWidth = DefaultWidth;

    /// <summary>与屏幕顶部边缘的空隙（逻辑像素）。</summary>
    private float _topGap = DefaultTopGap;

    public CapsuleBarWidget()
        : base((int)(MaxWidth + (ShadowMargin * 2)), (int)(PanelHeight + (ShadowMargin * 2)))
    {
        _vm = new DynamicIslandViewModel(
            App.Pomodoro,
            App.Audio,
            App.Hardware,
            App.Weather,
            App.ClipboardHistory,
            App.Data.Schedules,
            App.Data.Diaries,
            App.Data.Projects);

        _vm.PropertyChanged += OnViewModelChanged;
        App.Data.Changed += OnDataChanged;

        _vm.Start();
    }

    /// <summary>卡片相对窗口左上角的内缩。</summary>
    protected override float LayoutInset => ShadowMargin;

    /// <summary>
    /// 胶囊栏不接受外部坐标：它固定在主屏顶部居中，位置由「宽度」与「与顶部的空隙」
    /// 两个设置算出来，传进来的 x / y 会被忽略。
    /// </summary>
    public override void ApplyLayout(double x, double y)
    {
        var settings = App.Settings.Current.DynamicIsland;

        _cardWidth = (float)Math.Clamp(
            settings.Width >= MinWidth ? settings.Width : DefaultWidth, MinWidth, MaxWidth);
        _topGap = (float)Math.Max(0, settings.TopGap);

        // 宽度变了必须连窗口一起改：窗口与 DIB 不同步会把内容裁掉。
        Resize((int)(_cardWidth + (ShadowMargin * 2)), (int)(PanelHeight + (ShadowMargin * 2)));

        var scale = Scale;
        var cardWidthPx = (int)Math.Round(_cardWidth * scale);
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var left = Math.Max(0, (screenWidth - cardWidthPx) / 2);
        var top = (int)Math.Round(_topGap * scale);

        base.ApplyLayout(left, top);
    }

    /// <summary>当前是否为展开状态。</summary>
    public bool IsExpanded => _vm.IsExpanded;

    /// <inheritdoc />
    protected override void OnApplyTheme(bool isLight)
    {
        _theme = WidgetTheme.From(isLight);
        Redraw();
    }

    /// <inheritdoc />
    protected override void OnDraw(SKCanvas canvas, SKSize size)
    {
        var card = CardRect();

        WidgetPainting.DrawShadow(canvas, card, Radius,
            blur: 16f, alpha: _theme.IsLight ? 0.26f : 0.48f, offsetY: 3f);
        WidgetPainting.FillCard(canvas, card, Radius, _theme.Card);
        WidgetPainting.StrokeCard(canvas, card, Radius, _theme.Stroke);

        // 面板内容可能超出卡片，统一裁进圆角里；窄条态不会用到裁剪。
        using var clip = WidgetPainting.RoundedRect(card, Radius);
        canvas.Save();
        canvas.ClipPath(clip, SKClipOperation.Intersect, true);

        DrawBar(canvas, card);

        if (_expand > 0.01f)
        {
            DrawPanel(canvas, card);
        }

        canvas.Restore();
    }

    // ────────────────────────────── 窄条 ──────────────────────────────

    private void DrawBar(SKCanvas canvas, SKRect card)
    {
        var centerY = card.Top + (BarHeight / 2f);
        var baseline = centerY + 4f;

        // 左：今日待办
        var left = card.Left + 16f;
        WidgetPainting.DrawGlyph(canvas, "\uE8FD", left, baseline, _theme.TextSubtle, 12f);
        left += WidgetPainting.MeasureGlyph("\uE8FD", 12f) + 5f;
        WidgetPainting.DrawText(canvas, _vm.PendingText, left, baseline, _theme.TextPrimary, 12f);

        // 右：时间
        var right = card.Right - 16f;
        var timeWidth = WidgetPainting.MeasureText(_vm.TimeText, 14f, bold: true);
        WidgetPainting.DrawText(canvas, _vm.TimeText, right - timeWidth, baseline,
            _theme.TextPrimary, 14f, bold: true);

        // 中：番茄钟优先，否则天气；两者都按可用宽度居中排布
        var availableRight = right - timeWidth - 12f;
        var availableLeft = left + 12f;

        if (_vm.IsPomodoroActive)
        {
            var glyphWidth = WidgetPainting.MeasureGlyph("\uE916", 12f);
            var textWidth = WidgetPainting.MeasureText(_vm.PomodoroText, 12f);
            var total = glyphWidth + 5f + textWidth;
            var start = availableLeft + Math.Max(0f, ((availableRight - availableLeft) - total) / 2f);

            if (total <= availableRight - availableLeft)
            {
                WidgetPainting.DrawGlyph(canvas, "\uE916", start, baseline, _theme.TextSubtle, 12f);
                WidgetPainting.DrawText(canvas, _vm.PomodoroText, start + glyphWidth + 5f, baseline,
                    _theme.TextPrimary, 12f);
            }
        }
        else if (_vm.HasWeather)
        {
            var width = availableRight - availableLeft;
            if (width > 20f)
            {
                WidgetPainting.DrawText(canvas, _vm.WeatherText,
                    availableLeft + (width / 2f), baseline, _theme.TextPrimary, 12f,
                    align: SKTextAlign.Center, maxWidth: width);
            }
        }

        // 悬停提示：窄条提亮一点点，暗示「这里可以点开」。
        if (_hoverBar)
        {
            WidgetPainting.FillCard(canvas,
                new SKRect(card.Left, card.Top, card.Right, card.Top + BarHeight), Radius,
                WidgetTheme.Fade(_theme.TextPrimary, 0x0E));
        }

        // 展开面板与窄条之间的分隔线
        if (_expand > 0.5f)
        {
            WidgetPainting.DrawLine(canvas,
                card.Left + 12f, card.Top + BarHeight,
                card.Right - 12f, card.Top + BarHeight,
                _theme.Divider);
        }
    }

    // ────────────────────────────── 面板 ──────────────────────────────

    private void DrawPanel(SKCanvas canvas, SKRect card)
    {
        var alpha = (byte)Math.Clamp((int)(_expand * 255f), 0, 255);

        // 面板内容必须裁在顶部窄条【以下】：滚动时第一张卡片会上移，
        // 不裁就会画到窄条上面，看起来就是「胶囊栏被面板盖住了」。
        canvas.Save();
        canvas.ClipRect(
            new SKRect(card.Left, card.Top + BarHeight, card.Right, card.Bottom),
            SKClipOperation.Intersect,
            true);

        foreach (var (kind, rect) in LayoutCards())
        {
            if (rect.Bottom < card.Top + BarHeight || rect.Top > card.Bottom)
            {
                continue;
            }

            DrawCard(canvas, kind, rect, alpha);
        }

        // 底部提示
        var contentHeight = MeasureCards();
        if (contentHeight <= PanelHeight - BarHeight - PanelPad)
        {
            WidgetPainting.DrawText(canvas, App.Loc["IslandCollapseHint"],
                card.MidX, card.Bottom - 14f,
                WidgetTheme.Fade(_theme.TextSubtle, alpha), 11f, align: SKTextAlign.Center);
        }

        canvas.Restore();
    }

    private void DrawCard(SKCanvas canvas, CardKind kind, SKRect rect, byte alpha)
    {
        WidgetPainting.FillCard(canvas, rect, CardRadius, WidgetTheme.Fade(_theme.CardHover, alpha));
        WidgetPainting.StrokeCard(canvas, rect, CardRadius, WidgetTheme.Fade(_theme.Stroke, alpha));

        var left = rect.Left + CardPad;
        var right = rect.Right - CardPad;
        var top = rect.Top + CardPad;
        var primary = WidgetTheme.Fade(_theme.TextPrimary, alpha);
        var subtle = WidgetTheme.Fade(_theme.TextSubtle, alpha);

        switch (kind)
        {
            case CardKind.Schedule:
                DrawCardTitle(canvas, App.Loc["IslandTodaySchedule"], left, top, primary);
                DrawScheduleRows(canvas, left, right, top + TitleHeight + InnerGap, primary, subtle, alpha);
                break;

            case CardKind.Diary:
                DrawCardTitle(canvas, App.Loc["IslandRecentDiary"], left, top, primary);
                DrawDiaryRows(canvas, left, right, top + TitleHeight + InnerGap, primary, subtle, alpha);
                break;

            case CardKind.Weather:
                DrawCardTitle(canvas, App.Loc["IslandWeather"], left, top, primary);
                WidgetPainting.DrawText(canvas, _vm.WeatherText, left, top + TitleHeight + 26f,
                    primary, 22f, bold: true, maxWidth: right - left);
                WidgetPainting.DrawText(canvas, _vm.WeatherCity, left, top + TitleHeight + 46f,
                    subtle, 12f, maxWidth: right - left);
                break;

            case CardKind.Pomodoro:
                DrawPomodoroCard(canvas, rect, left, right, top, primary, subtle, alpha);
                break;

            case CardKind.Workbench:
                DrawCardTitle(canvas, App.Loc["WorkbenchTitle"], left, top, primary);
                DrawProjectRows(canvas, left, right, top + TitleHeight + InnerGap, primary, subtle, alpha);
                break;

            case CardKind.Hardware:
                DrawHardwareCard(canvas, left, right, top, primary, subtle, alpha);
                break;

            case CardKind.Clipboard:
                DrawCardTitle(canvas, App.Loc["IslandClipboard"], left, top, primary);
                DrawClipboardRows(canvas, left, right, top + TitleHeight + InnerGap, primary, subtle, alpha);
                break;
        }
    }

    private void DrawCardTitle(SKCanvas canvas, string text, float left, float top, SKColor color)
        => WidgetPainting.DrawText(canvas, text, left, top + 14f, color, 13f, bold: true);

    private void DrawScheduleRows(
        SKCanvas canvas, float left, float right, float top, SKColor primary, SKColor subtle, byte alpha)
    {
        if (_vm.AllSchedules.Count == 0)
        {
            WidgetPainting.DrawText(canvas, App.Loc["IslandNoScheduleToday"], left, top + 14f, subtle, 12f);
            return;
        }

        for (var i = 0; i < _vm.AllSchedules.Count; i++)
        {
            var item = _vm.AllSchedules[i];
            var y = top + (i * ScheduleRow);
            var rowRect = new SKRect(left - 4f, y, right + 4f, y + ScheduleRow - 2f);

            if (_hoverSchedule == i)
            {
                WidgetPainting.FillCard(canvas, rowRect, 6f, WidgetTheme.Fade(_theme.TextPrimary, (byte)(0x12 * alpha / 255)));
            }

            var textColor = item.IsCompleted
                ? WidgetTheme.Fade(primary, 0x66)
                : primary;

            DrawCheckbox(canvas, new SKPoint(left + 7f, y + 15f), item.IsCompleted, alpha);

            var textLeft = left + 22f;
            var width = right - textLeft;
            WidgetPainting.DrawText(canvas, item.Title, textLeft, y + 14f, textColor, 13f, maxWidth: width);

            WidgetPainting.DrawText(canvas, item.RangeText, textLeft, y + 30f, subtle, 11f, maxWidth: width);

            if (item.IsCompleted)
            {
                var titleWidth = Math.Min(WidgetPainting.MeasureText(item.Title, 13f), width);
                WidgetPainting.DrawLine(canvas, textLeft, y + 10f, textLeft + titleWidth, y + 10f,
                    WidgetTheme.Fade(primary, 0x66));
            }
        }
    }

    private void DrawDiaryRows(
        SKCanvas canvas, float left, float right, float top, SKColor primary, SKColor subtle, byte alpha)
    {
        if (_vm.RecentDiaries.Count == 0)
        {
            WidgetPainting.DrawText(canvas, App.Loc["DiaryEmpty"], left, top + 14f, subtle, 12f);
            return;
        }

        for (var i = 0; i < _vm.RecentDiaries.Count; i++)
        {
            var entry = _vm.RecentDiaries[i];
            var y = top + (i * DiaryRow);
            WidgetPainting.DrawText(canvas, entry.Title, left, y + 13f, primary, 13f, maxWidth: right - left);
            WidgetPainting.DrawText(canvas, entry.DateText, left, y + 27f, subtle, 11f, maxWidth: right - left);
        }
    }

    private void DrawProjectRows(
        SKCanvas canvas, float left, float right, float top, SKColor primary, SKColor subtle, byte alpha)
    {
        if (_vm.Projects.Count == 0)
        {
            WidgetPainting.DrawText(canvas, App.Loc["IslandNoProject"], left, top + 14f, subtle, 12f);
            return;
        }

        for (var i = 0; i < _vm.Projects.Count; i++)
        {
            var project = _vm.Projects[i];
            var y = top + (i * ProjectRow);
            var center = new SKPoint(left + 23f, y + 23f);

            DrawRing(canvas, center, 21f, project.Progress, project.ProgressText, primary, alpha);

            var textLeft = left + 56f;
            var width = right - textLeft;
            WidgetPainting.DrawText(canvas, project.Name, textLeft, y + 20f, primary, 13f, maxWidth: width);
            WidgetPainting.DrawText(canvas, project.VersionText, textLeft, y + 36f, subtle, 11f, maxWidth: width);
        }
    }

    private void DrawHardwareCard(
        SKCanvas canvas, float left, float right, float top, SKColor primary, SKColor subtle, byte alpha)
    {
        DrawCardTitle(canvas, App.Loc["IslandHardware"], left, top, primary);

        var y = top + TitleHeight + InnerGap;

        // 只给文字与数值，不画进度条：这一栏是「扫一眼看个数」，
        // 进度条既占宽度，也没比百分比多给任何信息。
        DrawMetricRow(canvas, left, y, "CPU", _vm.CpuText, primary);
        y += HardwareRow + 8f;

        DrawMetricRow(canvas, left, y, App.Loc["MemoryLabel"], $"{_vm.MemoryPercent:F0}%", primary);
        y += HardwareRow + 8f;

        DrawMetricRow(canvas, left, y, App.Loc["DiskLabel"], _vm.DiskText, primary);
        y += HardwareRow + 8f;

        DrawMetricRow(canvas, left, y, App.Loc["GpuLabel"], _vm.GpuText, primary);

        y += HardwareRow + 10f;

        if (_vm.HasBattery)
        {
            // 电池保留图标：它表达的是「有没有在充电」，一个数字说不清楚。
            WidgetPainting.DrawGlyph(canvas, _vm.BatteryGlyph, left, y + 11f, subtle, 14f);
            WidgetPainting.DrawText(canvas, _vm.BatteryText, left + 20f, y + 11f, subtle, 11f);
        }
    }

    /// <summary>
    /// 一行「标签 数值」。数值紧跟在标签右侧而不是右对齐到卡片边缘：
    /// 这样一行占的宽度只由内容决定，后面还可以继续往右排别的指标。
    /// </summary>
    private static void DrawMetricRow(
        SKCanvas canvas, float left, float y, string label, string valueText, SKColor color)
    {
        var labelWidth = WidgetPainting.DrawText(canvas, label, left, y + 13f, color, 12f);
        WidgetPainting.DrawText(canvas, valueText, left + labelWidth + 8f, y + 13f, color, 12f);
    }

    private void DrawPomodoroCard(
        SKCanvas canvas, SKRect rect, float left, float right, float top,
        SKColor primary, SKColor subtle, byte alpha)
    {
        DrawCardTitle(canvas, App.Loc["PomodoroTitle"], left, top, primary);
        WidgetPainting.DrawText(canvas, _vm.PomodoroText, left, top + TitleHeight + 26f, primary, 22f, bold: true);

        var centerY = rect.MidY;
        var cursor = right;

        if (_vm.CanExitPomodoro)
        {
            var width = MeasureButton(App.Loc["PomodoroExit"]);
            cursor -= width;
            DrawButton(canvas, App.Loc["PomodoroExit"],
                new SKRect(cursor, centerY - (ButtonHeight / 2f), cursor + width, centerY + (ButtonHeight / 2f)),
                primary, alpha, isPrimary: false, hover: _hoverExit);
            cursor -= 6f;
        }

        var mainWidth = MeasureButton(_vm.PomodoroPrimaryText);
        cursor -= mainWidth;
        DrawButton(canvas, _vm.PomodoroPrimaryText,
            new SKRect(cursor, centerY - (ButtonHeight / 2f), cursor + mainWidth, centerY + (ButtonHeight / 2f)),
            primary, alpha, isPrimary: true, hover: _hoverPrimary);
    }

    private void DrawClipboardRows(
        SKCanvas canvas, float left, float right, float top, SKColor primary, SKColor subtle, byte alpha)
    {
        var history = _vm.Clipboard.History;

        if (history.Count == 0)
        {
            WidgetPainting.DrawText(canvas, App.Loc["IslandClipboardEmpty"], left, top + 14f, subtle, 12f);
            return;
        }

        for (var i = 0; i < history.Count; i++)
        {
            var text = history[i];
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var y = top + (i * ClipboardRow);
            var buttonLeft = right - 24f;
            var width = buttonLeft - left - 8f;

            if (width <= 10f)
            {
                break;
            }

            // 剪切板内容按单行截断显示：多行折行需要逐字测量，每秒重绘下代价不划算。
            WidgetPainting.DrawText(canvas, text.ReplaceLineEndings(" "), left, y + 13f, primary, 11f, maxWidth: width);

            var buttonRect = new SKRect(buttonLeft, y + 1f, right, y + 21f);
            WidgetPainting.FillCard(canvas, buttonRect, 5f,
                _hoverClipboard == i
                    ? WidgetTheme.Fade(_theme.TextPrimary, (byte)(0x1F * alpha / 255))
                    : WidgetTheme.Fade(_theme.TextPrimary, (byte)(0x12 * alpha / 255)));

            WidgetPainting.DrawGlyph(canvas, "\uE8C8", buttonRect.MidX, buttonRect.MidY + 4f,
                WidgetTheme.Fade(subtle, alpha), 11f, SKTextAlign.Center);
        }
    }

    // ────────────────────────────── 绘制小件 ──────────────────────────────

    private void DrawCheckbox(SKCanvas canvas, SKPoint center, bool completed, byte alpha)
    {
        var rect = new SKRect(center.X - 7f, center.Y - 7f, center.X + 7f, center.Y + 7f);

        if (!completed)
        {
            WidgetPainting.StrokeCard(canvas, rect, 3f, WidgetTheme.Fade(_theme.TextPrimary, (byte)(0x59 * alpha / 255)));
            return;
        }

        WidgetPainting.FillCard(canvas, rect, 3f, WidgetTheme.Fade(_theme.Accent, alpha));

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f,
            StrokeCap = SKStrokeCap.Round,
            Color = SKColors.White,
        };

        using var path = new SKPath();
        path.MoveTo(rect.Left + 3.5f, center.Y);
        path.LineTo(center.X - 1f, rect.Bottom - 4f);
        path.LineTo(rect.Right - 3f, rect.Top + 4f);
        canvas.DrawPath(path, paint);
    }

    private void DrawRing(
        SKCanvas canvas, SKPoint center, float radius, double percent, string label, SKColor primary, byte alpha)
    {
        var oval = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

        using var track = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.5f,
            Color = WidgetTheme.Fade(_theme.TextPrimary, (byte)(0x1F * alpha / 255)),
        };
        canvas.DrawArc(oval, 0f, 360f, false, track);

        var sweep = (float)(Math.Clamp(percent, 0, 100) * 3.6);

        if (sweep > 0.5f)
        {
            using var arc = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3.5f,
                StrokeCap = SKStrokeCap.Round,
                Color = WidgetTheme.Fade(_theme.Accent, alpha),
            };
            canvas.DrawArc(oval, -90f, sweep, false, arc);
        }

        WidgetPainting.DrawText(canvas, label, center.X, center.Y + 4f, primary, 11f,
            bold: true, align: SKTextAlign.Center);
    }

    private static float MeasureButton(string text)
        => WidgetPainting.MeasureText(text, 12f) + 22f;

    private void DrawButton(
        SKCanvas canvas, string text, SKRect rect, SKColor textColor, byte alpha, bool isPrimary, bool hover)
    {
        var fill = isPrimary
            ? WidgetTheme.Fade(_theme.Accent, hover ? alpha : (byte)(0xD9 * alpha / 255))
            : WidgetTheme.Fade(_theme.TextPrimary, (byte)((hover ? 0x1F : 0x12) * alpha / 255));

        WidgetPainting.FillCard(canvas, rect, 6f, fill);

        WidgetPainting.DrawText(canvas, text, rect.MidX, rect.MidY + 4f,
            isPrimary ? SKColors.White : textColor, 12f, align: SKTextAlign.Center);
    }

    // ────────────────────────────── 版面计算 ──────────────────────────────

    private SKRect CardRect()
    {
        var height = BarHeight + ((PanelHeight - BarHeight) * _expand);

        return new SKRect(ShadowMargin, ShadowMargin, ShadowMargin + _cardWidth, ShadowMargin + height);
    }

    private IEnumerable<(CardKind Kind, SKRect Rect)> LayoutCards()
    {
        var card = CardRect();
        var left = card.Left + PanelPad;
        var width = _cardWidth - (PanelPad * 2);
        var y = card.Top + BarHeight + CardGap - _scrollOffset;

        foreach (var kind in CardOrder)
        {
            var height = MeasureCard(kind);
            yield return (kind, new SKRect(left, y, left + width, y + height));
            y += height + CardGap;
        }
    }

    private float MeasureCard(CardKind kind) => kind switch
    {
        CardKind.Schedule => (CardPad * 2) + TitleHeight + InnerGap
            + (Math.Max(1, _vm.AllSchedules.Count) * ScheduleRow),

        CardKind.Diary => (CardPad * 2) + TitleHeight + InnerGap
            + (Math.Max(1, _vm.RecentDiaries.Count) * DiaryRow),

        CardKind.Weather => (CardPad * 2) + TitleHeight + InnerGap + 20f + 20f,

        CardKind.Pomodoro => (CardPad * 2) + Math.Max(TitleHeight + 2f + 28f, ButtonHeight + 8f),

        CardKind.Workbench => (CardPad * 2) + TitleHeight + InnerGap
            + (Math.Max(1, _vm.Projects.Count) * ProjectRow),

        CardKind.Hardware => (CardPad * 2) + TitleHeight + InnerGap
            + (HardwareRow * 4) + 24f + (_vm.HasBattery ? HardwareRow + 10f : 0f),

        CardKind.Clipboard => (CardPad * 2) + TitleHeight + InnerGap
            + (Math.Max(1, _vm.Clipboard.History.Count) * ClipboardRow),

        _ => 60f,
    };

    private float MeasureCards()
    {
        var total = 0f;

        foreach (var kind in CardOrder)
        {
            total += MeasureCard(kind) + CardGap;
        }

        return total;
    }

    private float MaxScroll()
        => Math.Max(0f, MeasureCards() - (PanelHeight - BarHeight - PanelPad - CardGap));

    private SKRect CardRectFor(CardKind kind)
    {
        foreach (var (cardKind, rect) in LayoutCards())
        {
            if (cardKind == kind)
            {
                return rect;
            }
        }

        return SKRect.Empty;
    }

    // ────────────────────────────── 交互 ──────────────────────────────

    /// <inheritdoc />
    protected override bool HitTest(SKPoint point)
    {
        var card = CardRect();

        if (!card.Contains(point))
        {
            return false;
        }

        // 收起状态下只有窄条那一块可点，其余像素让点击穿到桌面。
        return _expand > 0.5f || point.Y <= card.Top + BarHeight;
    }

    /// <inheritdoc />
    protected override bool WantsHandCursor(SKPoint point)
    {
        if (HitBar(point))
        {
            return true;
        }

        return HitPrimaryButton(point) || HitExitButton(point)
            || HitScheduleRow(point) >= 0 || HitClipboardRow(point) >= 0;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(SKPoint point)
    {
        // 胶囊栏固定吸附在屏幕顶部居中，不接受拖动，这里只管悬停高亮。
        var bar = HitBar(point);
        var primary = HitPrimaryButton(point);
        var exit = HitExitButton(point);
        var schedule = HitScheduleRow(point);
        var clipboard = HitClipboardRow(point);

        if (bar != _hoverBar || primary != _hoverPrimary || exit != _hoverExit
            || schedule != _hoverSchedule || clipboard != _hoverClipboard)
        {
            _hoverBar = bar;
            _hoverPrimary = primary;
            _hoverExit = exit;
            _hoverSchedule = schedule;
            _hoverClipboard = clipboard;
            Redraw();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited()
    {
        if (!_hoverBar && !_hoverPrimary && !_hoverExit && _hoverSchedule < 0 && _hoverClipboard < 0)
        {
            return;
        }

        _hoverBar = false;
        _hoverPrimary = false;
        _hoverExit = false;
        _hoverSchedule = -1;
        _hoverClipboard = -1;
        Redraw();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(SKPoint point) => _pressed = true;

    /// <inheritdoc />
    protected override void OnPointerReleased(SKPoint point)
    {
        var wasPressed = _pressed;
        _pressed = false;

        if (!wasPressed)
        {
            return;
        }

        if (HitBar(point))
        {
            ToggleExpanded();
            return;
        }

        if (HitPrimaryButton(point))
        {
            if (App.Pomodoro.State == PomodoroState.Running)
            {
                App.Pomodoro.Pause();
            }
            else
            {
                App.Pomodoro.Start();
            }

            return;
        }

        if (HitExitButton(point))
        {
            App.Pomodoro.Reset();
            return;
        }

        var scheduleIndex = HitScheduleRow(point);
        if (scheduleIndex >= 0 && scheduleIndex < _vm.AllSchedules.Count)
        {
            var item = _vm.AllSchedules[scheduleIndex];
            item.IsCompleted = !item.IsCompleted;
            return;
        }

        var clipboardIndex = HitClipboardRow(point);
        if (clipboardIndex >= 0 && clipboardIndex < _vm.Clipboard.History.Count)
        {
            CopyToClipboard(_vm.Clipboard.History[clipboardIndex]);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(int delta, SKPoint point)
    {
        if (_expand < 0.5f)
        {
            return;
        }

        // 滚轮向上（delta 为正）时内容往下走。
        ScrollBy(-delta / 120f * WheelStep);
    }

    /// <inheritdoc />
    protected override void OnTick(double elapsedSeconds)
    {
        _animElapsed += elapsedSeconds;
        _expand = _animFrom + ((_animTo - _animFrom) * (float)WidgetSpring.Ease((float)_animElapsed));

        if (_animElapsed >= WidgetSpring.DurationSeconds)
        {
            _expand = _animTo;
            StopAnimation();
        }

        Redraw();
    }

    /// <summary>滚动面板内容。delta 为正表示向下滚。</summary>
    public void ScrollBy(float delta)
    {
        var max = MaxScroll();
        if (max <= 0f)
        {
            return;
        }

        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0f, max);
        Redraw();
    }

    private void ToggleExpanded() => SetExpanded(!_vm.IsExpanded);

    /// <summary>展开或收起面板。immediate 为 true 时跳过弹簧动画。</summary>
    public void SetExpanded(bool expanded, bool immediate = false)
    {
        if (_vm.IsExpanded == expanded)
        {
            return;
        }

        _vm.IsExpanded = expanded;

        if (immediate)
        {
            _expand = expanded ? 1f : 0f;
            _scrollOffset = 0f;
            StopAnimation();
            Redraw();
            return;
        }

        _animFrom = _expand;
        _animTo = expanded ? 1f : 0f;
        _animElapsed = 0;

        if (!expanded)
        {
            _scrollOffset = 0f;
        }

        StartAnimation();
    }

    // ────────────────────────────── 命中区域 ──────────────────────────────

    private bool HitBar(SKPoint point)
    {
        var card = CardRect();
        return point.X >= card.Left && point.X <= card.Right
            && point.Y >= card.Top && point.Y <= card.Top + BarHeight;
    }

    private bool HitPrimaryButton(SKPoint point) => HitPomodoroButton(point, primary: true);

    private bool HitExitButton(SKPoint point) => HitPomodoroButton(point, primary: false);

    private bool HitPomodoroButton(SKPoint point, bool primary)
    {
        if (_expand < 0.5f)
        {
            return false;
        }

        if (!primary && !_vm.CanExitPomodoro)
        {
            return false;
        }

        var rect = CardRectFor(CardKind.Pomodoro);
        if (rect.IsEmpty)
        {
            return false;
        }

        var right = rect.Right - CardPad;
        var centerY = rect.MidY;
        var cursor = right;

        if (_vm.CanExitPomodoro)
        {
            var exitWidth = MeasureButton(App.Loc["PomodoroExit"]);
            cursor -= exitWidth;

            if (!primary)
            {
                return new SKRect(cursor, centerY - (ButtonHeight / 2f), cursor + exitWidth, centerY + (ButtonHeight / 2f))
                    .Contains(point);
            }

            cursor -= 6f;
        }

        if (!primary)
        {
            return false;
        }

        var mainWidth = MeasureButton(_vm.PomodoroPrimaryText);
        cursor -= mainWidth;

        return new SKRect(cursor, centerY - (ButtonHeight / 2f), cursor + mainWidth, centerY + (ButtonHeight / 2f))
            .Contains(point);
    }

    private int HitScheduleRow(SKPoint point)
    {
        if (_expand < 0.5f || _vm.AllSchedules.Count == 0)
        {
            return -1;
        }

        var rect = CardRectFor(CardKind.Schedule);
        if (rect.IsEmpty)
        {
            return -1;
        }

        var top = rect.Top + CardPad + TitleHeight + InnerGap;
        var index = (int)((point.Y - top) / ScheduleRow);

        if (point.X < rect.Left + CardPad - 4f || point.X > rect.Right - CardPad + 4f)
        {
            return -1;
        }

        return index >= 0 && index < _vm.AllSchedules.Count ? index : -1;
    }

    private int HitClipboardRow(SKPoint point)
    {
        if (_expand < 0.5f)
        {
            return -1;
        }

        var count = _vm.Clipboard.History.Count;
        if (count == 0)
        {
            return -1;
        }

        var rect = CardRectFor(CardKind.Clipboard);
        if (rect.IsEmpty)
        {
            return -1;
        }

        var top = rect.Top + CardPad + TitleHeight + InnerGap;
        var index = (int)((point.Y - top) / ClipboardRow);

        // 只有右侧那枚复制按钮算可点区域，正文区域不响应点击。
        if (point.X < rect.Right - CardPad - 24f)
        {
            return -1;
        }

        return index >= 0 && index < count ? index : -1;
    }

    // ────────────────────────────── 杂项 ──────────────────────────────

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Log.Write("已从剪切板历史复制一条内容");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Log.Write($"写入剪贴板失败：{ex.Message}");
        }
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Redraw();

    private void OnDataChanged(object? sender, EventArgs e) => _vm.RefreshAll();

    /// <inheritdoc />
    protected override void OnClosing()
    {
        _vm.PropertyChanged -= OnViewModelChanged;
        App.Data.Changed -= OnDataChanged;
        _vm.Stop();
    }
}
