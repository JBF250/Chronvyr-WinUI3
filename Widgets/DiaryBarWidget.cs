using Chronvyr.Models;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;

namespace Chronvyr.Widgets;

/// <summary>
/// 桌面日记栏控件（自绘版，FR-5.2）。
/// 预览态只显示标题与日期，点击条目弹出独立日记详情窗口，右上角可快捷新建日记。
/// </summary>
public sealed class DiaryBarWidget : WidgetWindow
{
    // ---- 逻辑尺寸（物理尺寸由框架按 DPI 换算）----
    public const float CardWidth = 268f;
    public const float CardHeight = 400f;
    public const float CollapsedCardHeight = 46f;
    private const float Radius = 14f;

    /// <summary>阴影留边：卡片之外留出的绘制空间，不是窗口留白（窗口本身就画阴影）。</summary>
    private const float ShadowMargin = 16f;

    /// <summary>标题区高度，同时也是拖动把手区域。</summary>
    private const float HeaderHeight = 46f;

    private const float ListItemHeight = 52f;

    private readonly List<DiaryEntry> _entries = [];

    private WidgetTheme _theme = WidgetTheme.Dark;
    private bool _collapsed;
    private int _hoverIndex = -1;
    private bool _hoverHeader;
    private bool _hoverCollapse;
    private bool _hoverNew;
    private bool _dragging;
    private SKPoint _dragStartCursor;
    private int _dragStartWindowX;
    private int _dragStartWindowY;

    public DiaryBarWidget()
        : base((int)(CardWidth + (ShadowMargin * 2)), (int)(CardHeight + (ShadowMargin * 2)))
    {
        PositionChanged += OnPositionChanged;
        DiaryActivated += OnDiaryActivated;
        NewDiaryRequested += OnNewDiaryRequested;

        App.Data.Changed += OnDataChanged;
        Refresh(App.Data.Diaries);
    }

    /// <summary>拖动结束后汇报新位置（逻辑像素）。</summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>请求打开某条日记的详情。</summary>
    public event Action<DiaryEntry>? DiaryActivated;

    /// <summary>请求新建日记。</summary>
    public event Action? NewDiaryRequested;

    /// <summary>是否为折叠状态。</summary>
    public bool IsCollapsed => _collapsed;

    /// <summary>刷新数据，最近写的排在最上面。</summary>
    public void Refresh(IEnumerable<DiaryEntry> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries.OrderByDescending(entry => entry.Date));
        Redraw();
    }

    /// <summary>切换折叠。</summary>
    public void ToggleCollapse()
    {
        _collapsed = !_collapsed;

        Resize(
            (int)(CardWidth + (ShadowMargin * 2)),
            (int)((_collapsed ? CollapsedCardHeight : CardHeight) + (ShadowMargin * 2)));

        Redraw();
    }

    /// <inheritdoc />
    protected override float LayoutInset => ShadowMargin;

    /// <inheritdoc />
    protected override void OnApplyTheme(bool isLight)
    {
        _theme = WidgetTheme.From(isLight);
        Redraw();
    }

    /// <inheritdoc />
    protected override void OnDraw(SKCanvas canvas, SKSize size)
    {
        var card = CardRect(size);

        // 1. 投影：自绘，无需额外窗口，也不会被窗口区域裁掉。
        WidgetPainting.DrawShadow(canvas, card, Radius, blur: 16f, alpha: _theme.IsLight ? 0.28f : 0.5f, offsetY: 3f);

        // 2. 卡片本体。
        WidgetPainting.FillCard(canvas, card, Radius, _theme.Card);

        // 3. 描边：跟随主题，深色下极淡，不再形成一圈显眼亮边。
        WidgetPainting.StrokeCard(canvas, card, Radius, _theme.Stroke);

        // 4. 标题区（同时是拖动把手）。
        DrawHeader(canvas, card);

        if (_collapsed)
        {
            return;
        }

        // 5. 日记列表。
        DrawList(canvas, card);
    }

    private void DrawHeader(SKCanvas canvas, SKRect card)
    {
        WidgetPainting.DrawText(canvas, App.Loc["DiaryTitle"], card.Left + 16f, card.Top + 30f,
            _theme.TextPrimary, 15f, bold: true);

        // 折叠按钮
        var collapseCenter = new SKPoint(card.Right - 52f, card.Top + 23f);
        WidgetPainting.FillCircleIf(canvas, collapseCenter, 15f,
            _hoverCollapse ? WidgetTheme.Fade(_theme.TextPrimary, 0x1A) : SKColors.Transparent);
        DrawChevron(canvas, collapseCenter, _theme.TextSubtle);

        // 新建按钮
        var newCenter = new SKPoint(card.Right - 24f, card.Top + 23f);
        WidgetPainting.FillCircleIf(canvas, newCenter, 15f,
            _hoverNew ? WidgetTheme.Fade(_theme.TextPrimary, 0x1A) : SKColors.Transparent);
        WidgetPainting.DrawPlus(canvas, newCenter, _theme.TextSubtle);

        // 分隔线
        WidgetPainting.DrawLine(canvas,
            card.Left + 12f, card.Top + HeaderHeight,
            card.Right - 12f, card.Top + HeaderHeight,
            _theme.Divider);
    }

    private void DrawList(SKCanvas canvas, SKRect card)
    {
        if (_entries.Count == 0)
        {
            WidgetPainting.DrawText(canvas, App.Loc["DiaryEmpty"],
                card.MidX, card.Top + HeaderHeight + 40f, _theme.TextSubtle, 12f,
                align: SKTextAlign.Center);
            return;
        }

        var y = card.Top + HeaderHeight + 6f;
        var maxY = card.Bottom - 8f;

        for (var i = 0; i < _entries.Count; i++)
        {
            if (y + ListItemHeight > maxY)
            {
                break;
            }

            var entry = _entries[i];
            var rowRect = new SKRect(card.Left + 6f, y, card.Right - 6f, y + ListItemHeight - 4f);

            if (_hoverIndex == i)
            {
                WidgetPainting.FillCard(canvas, rowRect, 8f, WidgetTheme.Fade(_theme.TextPrimary, 0x12));
            }

            var available = rowRect.Width - 24f;

            WidgetPainting.DrawText(canvas, entry.Title, rowRect.Left + 12f, y + 22f,
                _theme.TextPrimary, 13f, maxWidth: available);

            WidgetPainting.DrawText(canvas, entry.DateText, rowRect.Left + 12f, y + 40f,
                _theme.TextSubtle, 11f, maxWidth: available);

            y += ListItemHeight;
        }
    }

    private void DrawChevron(SKCanvas canvas, SKPoint center, SKColor color)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.6f,
            StrokeCap = SKStrokeCap.Round,
            Color = color,
        };

        var w = 5f;
        var h = _collapsed ? -3f : 3f;
        using var path = new SKPath();
        path.MoveTo(center.X - w, center.Y - h);
        path.LineTo(center.X, center.Y + h);
        path.LineTo(center.X + w, center.Y - h);
        canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    protected override bool HitTest(SKPoint point) => CardRect(Size).Contains(point);

    /// <inheritdoc />
    protected override bool WantsHandCursor(SKPoint point)
        => HitCollapse(point) || HitNew(point) || IndexAt(point) >= 0;

    /// <inheritdoc />
    protected override void OnPointerMoved(SKPoint point)
    {
        if (_dragging)
        {
            if (NativeMethods.GetCursorPos(out var cursor))
            {
                Move(
                    _dragStartWindowX + (cursor.X - (int)_dragStartCursor.X),
                    _dragStartWindowY + (cursor.Y - (int)_dragStartCursor.Y));
            }

            return;
        }

        var index = IndexAt(point);
        var header = HitHeader(point);
        var collapse = HitCollapse(point);
        var newButton = HitNew(point);

        if (index != _hoverIndex || header != _hoverHeader || collapse != _hoverCollapse || newButton != _hoverNew)
        {
            _hoverIndex = index;
            _hoverHeader = header;
            _hoverCollapse = collapse;
            _hoverNew = newButton;
            Redraw();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited()
    {
        if (_hoverIndex >= 0 || _hoverHeader || _hoverCollapse || _hoverNew)
        {
            _hoverIndex = -1;
            _hoverHeader = false;
            _hoverCollapse = false;
            _hoverNew = false;
            Redraw();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(SKPoint point)
    {
        if (HitHeader(point) && NativeMethods.GetCursorPos(out var cursor))
        {
            _dragging = true;
            _dragStartCursor = new SKPoint(cursor.X, cursor.Y);
            var position = GetWindowPosition();
            _dragStartWindowX = position.X;
            _dragStartWindowY = position.Y;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(SKPoint point)
    {
        if (_dragging)
        {
            _dragging = false;
            var origin = GetCardOrigin();
            PositionChanged?.Invoke(origin.X, origin.Y);
            return;
        }

        if (HitCollapse(point))
        {
            ToggleCollapse();
            return;
        }

        if (HitNew(point))
        {
            NewDiaryRequested?.Invoke();
            return;
        }

        var index = IndexAt(point);
        if (index >= 0 && index < _entries.Count)
        {
            DiaryActivated?.Invoke(_entries[index]);
        }
    }

    /// <summary>取窗口当前左上角的物理像素坐标。</summary>
    private (int X, int Y) GetWindowPosition()
    {
        if (NativeMethods.GetWindowRect(Handle, out var rect))
        {
            return (rect.Left, rect.Top);
        }

        return (0, 0);
    }

    private static SKRect CardRect(SKSize size) => new(
        ShadowMargin,
        ShadowMargin,
        size.Width - ShadowMargin,
        size.Height - ShadowMargin);

    private bool HitHeader(SKPoint p)
    {
        var card = CardRect(Size);
        var header = new SKRect(card.Left, card.Top, card.Right, card.Top + HeaderHeight);
        return header.Contains(p) && !HitCollapse(p) && !HitNew(p);
    }

    private bool HitCollapse(SKPoint p)
    {
        var card = CardRect(Size);
        return Distance(p, new SKPoint(card.Right - 52f, card.Top + 23f)) <= 16f;
    }

    private bool HitNew(SKPoint p)
    {
        var card = CardRect(Size);
        return Distance(p, new SKPoint(card.Right - 24f, card.Top + 23f)) <= 16f;
    }

    private int IndexAt(SKPoint p)
    {
        if (_collapsed)
        {
            return -1;
        }

        var card = CardRect(Size);
        var listTop = card.Top + HeaderHeight + 6f;
        if (p.Y < listTop || p.X < card.Left + 6f || p.X > card.Right - 6f)
        {
            return -1;
        }

        var index = (int)((p.Y - listTop) / ListItemHeight);
        return index >= 0 && index < _entries.Count ? index : -1;
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>拖动结束后把新位置写回设置，下次启动仍在原处。</summary>
    private void OnPositionChanged(double x, double y)
    {
        var settings = App.Settings.Current.DiaryBar;
        settings.X = x;
        settings.Y = y;
    }

    /// <summary>点击日记 → 打开独立详情窗口。</summary>
    private void OnDiaryActivated(DiaryEntry entry) => Views.Dialogs.DiaryDetailWindow.Open(entry);

    /// <summary>
    /// 快捷新建日记（FR-5.2）：先弹对话框填写，点「创建」才落库。
    /// </summary>
    /// <remarks>
    /// 对话框必须挂在主窗口的 XamlRoot 上，主界面最小化时它无处可显示，
    /// 表现成「点了新建没反应」。这里先把主窗口恢复出来再弹。
    /// </remarks>
    private void OnNewDiaryRequested()
    {
        var mainWindow = App.MainWindowInstance;
        if (mainWindow?.Content?.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        Helpers.WindowHelper.Show(mainWindow);

        var entry = new DiaryEntry { Date = DateTimeOffset.Now };
        var dialog = new Views.Dialogs.DiaryEditDialog(entry, isNew: true) { XamlRoot = xamlRoot };

        _ = ShowNewDiaryDialogAsync(dialog, entry);
    }

    private async Task ShowNewDiaryDialogAsync(ContentDialog dialog, DiaryEntry entry)
    {
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            App.Data.Diaries.Add(entry);
            Refresh(App.Data.Diaries);
        }
    }

    private void OnDataChanged(object? sender, EventArgs e) => Refresh(App.Data.Diaries);

    /// <summary>窗口关闭时退订数据变更，避免控件销毁后仍被回调。</summary>
    protected override void OnClosing() => App.Data.Changed -= OnDataChanged;
}
