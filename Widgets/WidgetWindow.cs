using System.Runtime.InteropServices;
using SkiaSharp;

namespace Chronvyr.Widgets;

/// <summary>
/// 自绘桌面控件的窗口基类。
/// </summary>
/// <remarks>
/// 为什么不用 XAML：
/// WinUI 3 的窗口背景无法真正透明（由系统按「系统主题」绘制，<c>RequestedTheme</c> 管不到），
/// 于是卡片圆角之外必然露出一圈窗口底色——实测顶部有 4 物理像素纯白边；
/// 同时 XAML 也拿不到窗口投影。改用 Win32 分层窗口 + SkiaSharp 自绘后，
/// 圆角、阴影、透明度、任意尺寸全部由自己控制。
///
/// 渲染管线与参考项目 NotchPeninsula 一致：
/// Skia 直接画进 <c>CreateDIBSection</c> 的内存 → <c>UpdateLayeredWindow</c> 一次提交（零拷贝）。
/// </remarks>
public abstract class WidgetWindow : IDisposable, Controls.IDesktopWidget
{
    private const string WindowClassName = "Chronvyr.WidgetWindow";

    /// <summary>保持委托引用，否则会被 GC 回收导致窗口过程变成野指针。</summary>
    private static readonly NativeMethods.WndProcDelegate WndProcRef = StaticWndProc;

    private static readonly Dictionary<nint, WidgetWindow> Instances = [];

    private static bool _classRegistered;

    private const int AnimTimerId = 1;
    private const uint AnimIntervalMs = 15;

    private nint _hwnd;
    private nint _memDc;
    private nint _dib;
    private nint _oldBitmap;
    private nint _dibBits;

    private SKSurface? _surface;
    private int _width;
    private int _height;
    private float _scale = 1f;
    private bool _disposed;
    private bool _trackingMouse;
    private bool _animating;
    private long _animLast;
    private nint _handCursor;
    private nint _arrowCursor;

    /// <param name="logicalWidth">逻辑宽度（96 DPI 下的像素）。</param>
    /// <param name="logicalHeight">逻辑高度。</param>
    protected WidgetWindow(int logicalWidth, int logicalHeight)
    {
        // 尺寸一律按逻辑像素描述：窗口与位图都要按 DPI 放大，否则在高 DPI 下
        // 绘制空间会小于设计尺寸，卡片右下角会被裁掉一圈。
        var scale = NativeMethods.GetDpiForSystem() / 96f;
        _width = (int)Math.Round(logicalWidth * scale);
        _height = (int)Math.Round(logicalHeight * scale);

        EnsureClassRegistered();

        _hwnd = NativeMethods.CreateWindowEx(
            NativeMethods.WsExLayered | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate,
            WindowClassName,
            null,
            NativeMethods.WsPopup,
            0, 0, _width, _height,
            0, 0, NativeMethods.GetModuleHandle(null), 0);

        if (_hwnd == 0)
        {
            throw new InvalidOperationException($"创建控件窗口失败，Win32 错误码 {Marshal.GetLastWin32Error()}");
        }

        Instances[_hwnd] = this;
        _scale = NativeMethods.GetDpiForWindow(_hwnd) / 96f;

        _arrowCursor = NativeMethods.LoadCursor(0, NativeMethods.IdcArrow);
        _handCursor = NativeMethods.LoadCursor(0, NativeMethods.IdcHand);

        RecreateBuffers();
    }

    /// <summary>窗口句柄。</summary>
    public nint Handle => _hwnd;

    /// <summary>当前逻辑尺寸（物理像素 ÷ DPI 缩放）。</summary>
    public SKSize Size => new(_width / _scale, _height / _scale);

    /// <summary>DPI 缩放。</summary>
    public float Scale => _scale;

    /// <summary>物理尺寸。</summary>
    public int PixelWidth => _width;

    /// <summary>物理高度。</summary>
    public int PixelHeight => _height;

    /// <summary>显示窗口。</summary>
    public void Show() => NativeMethods.ShowWindow(_hwnd, 5);

    /// <summary>按屏幕物理像素移动。</summary>
    public void Move(int x, int y)
        => NativeMethods.SetWindowPos(_hwnd, 0, x, y, 0, 0,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoSize);

    /// <summary>移动（屏幕物理像素）并改尺寸（逻辑像素）。</summary>
    public void MoveAndResize(int x, int y, int logicalWidth, int logicalHeight)
    {
        var scale = _hwnd != 0 ? NativeMethods.GetDpiForWindow(_hwnd) / 96f : 1f;
        var width = (int)Math.Round(logicalWidth * scale);
        var height = (int)Math.Round(logicalHeight * scale);

        if (width != _width || height != _height)
        {
            _width = width;
            _height = height;
            RecreateBuffers();
        }

        NativeMethods.SetWindowPos(_hwnd, 0, x, y, width, height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    /// <summary>仅改尺寸（逻辑像素，内部按窗口 DPI 换算）。</summary>
    public void Resize(int logicalWidth, int logicalHeight)
    {
        var scale = _hwnd != 0 ? NativeMethods.GetDpiForWindow(_hwnd) / 96f : 1f;
        var width = (int)Math.Round(logicalWidth * scale);
        var height = (int)Math.Round(logicalHeight * scale);

        if (width == _width && height == _height)
        {
            return;
        }

        _width = width;
        _height = height;
        RecreateBuffers();

        // 只换缓冲区是不够的：窗口本身也得跟着改尺寸，否则新内容会被裁在旧窗口里，
        // 折叠这类「只改高矮」的操作看上去就完全没反应。
        NativeMethods.SetWindowPos(_hwnd, 0, 0, 0, width, height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpNoMove);

        Redraw();
    }

    /// <summary>把窗口压到 Z 序最底部（壁纸之上、普通窗口之下）。</summary>
    public void SendToBottom()
        => NativeMethods.SetWindowPos(_hwnd, 1, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);

    /// <summary>
    /// 把控件插到指定窗口的【正下方】。
    /// 用它而不是 <see cref="SendToBottom"/>：后者是 HWND_BOTTOM，会把控件压到壁纸层，
    /// 直接看不见了；插到主窗口下面才能既低于主界面、又不掉出可视范围。
    /// </summary>
    public void SendBehind(nint hwnd)
    {
        if (_hwnd == 0 || hwnd == 0 || hwnd == _hwnd)
        {
            return;
        }

        NativeMethods.SetWindowPos(_hwnd, hwnd, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    /// <summary>
    /// 把当前渲染结果导出为 PNG。
    /// </summary>
    /// <remarks>
    /// 用于验证渲染是否正确 —— 屏幕截图会被其他窗口遮挡，在自动化环境里不可靠；
    /// 直接导出 Skia 表面则完全绕开这个问题。
    /// </remarks>
    public void SaveSnapshot(string path)
    {
        if (_surface is null)
        {
            return;
        }

        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>重绘并提交。</summary>
    public void Redraw()
    {
        if (_disposed || _surface is null || _hwnd == 0)
        {
            return;
        }

        var canvas = _surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // 以逻辑像素绘制，Skia 负责按 DPI 放大。
        canvas.Save();
        canvas.Scale(_scale);
        OnDraw(canvas, new SKSize(_width / _scale, _height / _scale));
        canvas.Restore();

        Present();
    }

    /// <summary>关闭并释放。</summary>
    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OnClosing();
        ReleaseBuffers();

        if (_hwnd != 0)
        {
            Instances.Remove(_hwnd);
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>关闭控件窗口，供桌面控件管理器调用。</summary>
    public void CloseWidget() => Close();

    /// <summary>
    /// 卡片相对窗口左上角的内缩（逻辑像素）。窗口要留出阴影绘制空间，卡片原点并不在
    /// 窗口左上角；而设置里存的是**卡片**的位置，所以摆放与回报位置都必须算上这段内缩，
    /// 否则拖动一次位置就会漂移。
    /// </summary>
    protected virtual float LayoutInset => 0f;

    /// <summary>
    /// 按设置中的 X / Y（卡片左上角，屏幕物理像素）摆放控件。
    /// 允许子类覆写：胶囊栏不接受外部坐标，位置由它自己按屏幕宽度算出来。
    /// </summary>
    public virtual void ApplyLayout(double x, double y)
    {
        var inset = (int)Math.Round(LayoutInset * Scale);
        Move((int)Math.Round(x) - inset, (int)Math.Round(y) - inset);
    }

    /// <summary>取卡片左上角当前的物理屏幕坐标（拖动结束后回写设置用）。</summary>
    protected (int X, int Y) GetCardOrigin()
    {
        var inset = (int)Math.Round(LayoutInset * Scale);

        if (NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return (rect.Left + inset, rect.Top + inset);
        }

        return (0, 0);
    }

    /// <summary>窗口关闭前的清理钩子（子类用来退订事件）。</summary>
    protected virtual void OnClosing()
    {
    }

    /// <summary>切换浅色/深色配色，由注册表与设置页统一调用。</summary>
    public void ApplyTheme(bool light) => OnApplyTheme(light);

    /// <summary>主题变化时重写此方法换色并重绘。</summary>
    protected virtual void OnApplyTheme(bool light)
    {
    }

    /// <summary>在此绘制控件内容（坐标已是逻辑像素）。</summary>
    protected abstract void OnDraw(SKCanvas canvas, SKSize size);

    /// <summary>命中测试：返回 false 时鼠标事件会穿透到下面的窗口。</summary>
    protected virtual bool HitTest(SKPoint point) => true;

    /// <summary>鼠标进入可交互区域。</summary>
    protected virtual void OnPointerEntered(SKPoint point)
    {
    }

    /// <summary>鼠标在窗口内移动。</summary>
    protected virtual void OnPointerMoved(SKPoint point)
    {
    }

    /// <summary>鼠标离开窗口。</summary>
    protected virtual void OnPointerExited()
    {
    }

    /// <summary>左键按下。</summary>
    protected virtual void OnPointerPressed(SKPoint point)
    {
    }

    /// <summary>左键抬起。</summary>
    protected virtual void OnPointerReleased(SKPoint point)
    {
    }

    /// <summary>鼠标滚轮。delta 为正表示向上滚。</summary>
    protected virtual void OnPointerWheel(int delta, SKPoint point)
    {
    }

    /// <summary>是否把鼠标指针显示为手型（表示可点击）。</summary>
    protected virtual bool WantsHandCursor(SKPoint point) => false;

    /// <summary>当前是否处于动画节拍中。</summary>
    protected bool IsAnimating => _animating;

    /// <summary>
    /// 启动渲染节拍。窗口只在真正需要动画时才占用定时器，静止时零开销，
    /// 因此多个控件同时存在也不会白白烧 CPU。约 15ms 一拍（≈66fps）。
    /// </summary>
    protected void StartAnimation()
    {
        if (_animating || _hwnd == 0)
        {
            return;
        }

        _animating = true;
        _animLast = Environment.TickCount64;
        NativeMethods.SetTimer(_hwnd, AnimTimerId, AnimIntervalMs, 0);
    }

    /// <summary>停止渲染节拍（动画收敛后必须调用）。</summary>
    protected void StopAnimation()
    {
        if (!_animating)
        {
            return;
        }

        _animating = false;
        NativeMethods.KillTimer(_hwnd, AnimTimerId);
    }

    /// <summary>每帧回调，参数为距上一帧的秒数。</summary>
    protected virtual void OnTick(double elapsedSeconds)
    {
    }

    // ---- 内部实现 ----

    private static void EnsureClassRegistered()
    {
        if (_classRegistered)
        {
            return;
        }

        var wc = new NativeMethods.WndClassEx
        {
            CbSize = Marshal.SizeOf<NativeMethods.WndClassEx>(),
            Style = 0,
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcRef),
            HInstance = NativeMethods.GetModuleHandle(null),
            LpszClassName = WindowClassName,
        };

        _ = NativeMethods.RegisterClassEx(ref wc);
        _classRegistered = true;
    }

    private static nint StaticWndProc(nint hWnd, int msg, nint wParam, nint lParam)
    {
        if (Instances.TryGetValue(hWnd, out var widget))
        {
            return widget.WndProc(msg, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private nint WndProc(int msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case NativeMethods.WmPaint:
                Redraw();
                return 0;

            case NativeMethods.WmMouseMove:
            {
                if (!_trackingMouse)
                {
                    // 必须显式请求才会收到 WM_MOUSELEAVE，否则鼠标移出后
                    // 状态会一直停在「已进入」，悬停效果无法复位。
                    _trackingMouse = true;
                    var track = new NativeMethods.TrackMouseEventStruct
                    {
                        CbSize = Marshal.SizeOf<NativeMethods.TrackMouseEventStruct>(),
                        DwFlags = NativeMethods.TmeLeave,
                        HwndTrack = _hwnd,
                    };
                    _ = NativeMethods.TrackMouseEvent(ref track);
                    OnPointerEntered(ToLogical(lParam));
                }

                var point = ToLogical(lParam);
                NativeMethods.SetCursor(HitTest(point) && WantsHandCursor(point) ? _handCursor : _arrowCursor);
                OnPointerMoved(point);
                return 0;
            }

            case NativeMethods.WmTimer:
            {
                if ((int)wParam == AnimTimerId && _animating)
                {
                    var now = Environment.TickCount64;
                    var dt = Math.Clamp((now - _animLast) / 1000.0, 0.0, 0.1);
                    _animLast = now;
                    OnTick(dt);
                }

                return 0;
            }

            case NativeMethods.WmMouseLeave:
                _trackingMouse = false;
                OnPointerExited();
                return 0;

            case NativeMethods.WmLButtonDown:
                OnPointerPressed(ToLogical(lParam));
                return 0;

            case NativeMethods.WmLButtonUp:
                OnPointerReleased(ToLogical(lParam));
                return 0;

            case NativeMethods.WmMouseWheel:
            {
                // WM_MOUSEWHEEL 的 lParam 和 WM_NCHITTEST 一样是【屏幕】坐标，
                // 滚轮增量在 wParam 的高 16 位，且是有符号数。
                var delta = (short)((long)wParam >> 16);
                OnPointerWheel(delta, ToLogicalFromScreen(lParam));
                return 0;
            }

            case NativeMethods.WmNchitTest:
            {
                // 命中测试落在空白处时返回 HTTRANSPARENT，让点击穿透到下层窗口。
                // 注意 WM_NCHITTEST 的 lParam 是【屏幕】坐标，与鼠标消息不同。
                var screenPoint = ToLogicalFromScreen(lParam);
                return HitTest(screenPoint) ? NativeMethods.HtClient : NativeMethods.HtTransparent;
            }

            case NativeMethods.WmDpiChanged:
                _scale = NativeMethods.GetDpiForWindow(_hwnd) / 96f;
                RecreateBuffers();
                Redraw();
                return 0;

            case NativeMethods.WmDestroy:
                Instances.Remove(_hwnd);
                return 0;

            default:
                return NativeMethods.DefWindowProc(_hwnd, msg, wParam, lParam);
        }
    }

    /// <summary>
    /// 鼠标消息的 lParam 是【客户区】坐标（相对窗口左上角），已是物理像素，
    /// 除以缩放即得 Skia 绘制用的逻辑坐标。
    /// </summary>
    private SKPoint ToLogical(nint lParam)
    {
        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        return new SKPoint(x / _scale, y / _scale);
    }

    /// <summary>WM_NCHITTEST 的 lParam 是【屏幕】坐标，需先减去窗口原点。</summary>
    private SKPoint ToLogicalFromScreen(nint lParam)
    {
        var screenX = unchecked((short)(long)lParam);
        var screenY = unchecked((short)((long)lParam >> 16));

        if (NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return new SKPoint((screenX - rect.Left) / _scale, (screenY - rect.Top) / _scale);
        }

        return new SKPoint(screenX / _scale, screenY / _scale);
    }

    private void RecreateBuffers()
    {
        ReleaseBuffers();

        if (_width <= 0 || _height <= 0)
        {
            return;
        }

        var screenDc = NativeMethods.GetDC(0);
        _memDc = NativeMethods.CreateCompatibleDC(screenDc);

        var bmi = new NativeMethods.BitmapInfo
        {
            Header = new NativeMethods.BitmapInfoHeader
            {
                BiSize = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                BiWidth = _width,
                BiHeight = -_height, // 负数 = 自上而下，与 Skia 的行序一致
                BiPlanes = 1,
                BiBitCount = 32,
                BiCompression = 0,
            },
        };

        _dib = NativeMethods.CreateDIBSection(screenDc, ref bmi, NativeMethods.DibRgbColors, out _dibBits, 0, 0);
        _oldBitmap = NativeMethods.SelectObject(_memDc, _dib);
        _ = NativeMethods.ReleaseDC(0, screenDc);

        var info = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info, _dibBits, _width * 4);
    }

    private void ReleaseBuffers()
    {
        _surface?.Dispose();
        _surface = null;

        if (_memDc != 0)
        {
            if (_oldBitmap != 0)
            {
                _ = NativeMethods.SelectObject(_memDc, _oldBitmap);
                _oldBitmap = 0;
            }

            _ = NativeMethods.DeleteDC(_memDc);
            _memDc = 0;
        }

        if (_dib != 0)
        {
            _ = NativeMethods.DeleteObject(_dib);
            _dib = 0;
        }

        _dibBits = 0;
    }

    private void Present()
    {
        if (_memDc == 0)
        {
            return;
        }

        var screenDc = NativeMethods.GetDC(0);

        // UpdateLayeredWindow 的 pptDst 是【新的屏幕位置】，不是相对偏移：
        // 传 (0,0) 会把窗口直接拽到屏幕左上角，所以必须回传窗口当前位置。
        var dst = new NativeMethods.Point(0, 0);
        if (NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            dst = new NativeMethods.Point(rect.Left, rect.Top);
        }

        var size = new NativeMethods.Size(_width, _height);
        var src = new NativeMethods.Point(0, 0);
        var blend = NativeMethods.BlendFunction.Alpha;

        _ = NativeMethods.UpdateLayeredWindow(
            _hwnd, screenDc, ref dst, ref size, _memDc, ref src, 0, ref blend, NativeMethods.UlwAlpha);

        _ = NativeMethods.ReleaseDC(0, screenDc);
    }
}
