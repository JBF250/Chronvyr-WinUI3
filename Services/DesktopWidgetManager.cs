using Chronvyr.Controls;
using Chronvyr.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Chronvyr.Services;

/// <summary>
/// 桌面控件管理器（FR-7.2）：按设置创建 / 更新 / 销毁日记栏、日程表、胶囊栏，
/// 并在设置变化时实时同步位置与视觉风格。
/// </summary>
public sealed class DesktopWidgetManager
{
    private readonly AppSettings _settings;
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<DesktopWidgetKind, IDesktopWidget> _widgets = [];
    private readonly Dictionary<DesktopWidgetKind, VisualStyle> _appliedStyles = [];
    private bool _applying;

    public DesktopWidgetManager(AppSettings settings)
    {
        _settings = settings;

        settings.DiaryBar.PropertyChanged += (_, _) => Apply(DesktopWidgetKind.DiaryBar);
        settings.ScheduleBoard.PropertyChanged += (_, _) => Apply(DesktopWidgetKind.ScheduleBoard);
        settings.DynamicIsland.PropertyChanged += (_, _) => Apply(DesktopWidgetKind.DynamicIsland);
        settings.PropertyChanged += OnMainSettingsChanged;
    }

    /// <summary>按当前设置同步全部桌面控件。</summary>
    public void ApplyAll()
    {
        foreach (var kind in Enum.GetValues<DesktopWidgetKind>())
        {
            Apply(kind);
        }
    }

    /// <summary>同步单个桌面控件。任何控件侧异常都不会向外传播。</summary>
    public void Apply(DesktopWidgetKind kind)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            var widget = _settings.GetWidget(kind);

            if (!widget.IsEnabled)
            {
                if (_widgets.Remove(kind, out var closing))
                {
                    _appliedStyles.Remove(kind);
                    closing.CloseWidget();
                    Log.Write($"桌面控件已关闭: {kind}");
                }

                return;
            }

            if (!_widgets.TryGetValue(kind, out var target))
            {
                target = Create(kind);
                _widgets[kind] = target;
                ApplyStyle(target, kind);
                ActivateWidget(target);
                Log.Write($"桌面控件已创建: {kind}");
            }
            else
            {
                ApplyStyle(target, kind);
            }

            target.ApplyLayout(widget.X, widget.Y);

            // 刚建出来的控件要立刻压到主窗口下面，否则它会盖在主界面上。
            SendWidgetsBehind();
        }
        catch (Exception ex)
        {
            // 控件侧异常只记录，绝不向上传播：否则会中断设置页的主题切换调用链。
            Log.WriteException($"应用桌面控件 {kind} 失败", ex);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>
    /// 把所有自绘控件插到主窗口正下方。
    /// 控件是独立顶层窗口、创建时机比主窗口晚，不处理的话启动后会一直盖在主界面上
    /// ——所以主窗口每次激活都要重新压一次。
    /// </summary>
    public void SendWidgetsBehind()
    {
        var mainWindow = App.MainWindowInstance;
        if (mainWindow is null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);

        foreach (var widget in _widgets.Values)
        {
            if (widget is Widgets.WidgetWindow native)
            {
                native.SendBehind(handle);
            }
        }
    }

    /// <summary>让控件窗口显示出来。自绘控件是原生窗口，走 ShowWindow。</summary>
    private static void ActivateWidget(IDesktopWidget widget)
    {
        switch (widget)
        {
            case Window window:
                window.Activate();
                break;

            case Widgets.WidgetWindow native:
                native.Show();
                break;
        }
    }

    /// <summary>指定控件当前是否已创建。</summary>
    public bool IsCreated(DesktopWidgetKind kind) => _widgets.ContainsKey(kind);

    private static IDesktopWidget Create(DesktopWidgetKind kind) => kind switch
    {
        DesktopWidgetKind.DiaryBar => new Widgets.DiaryBarWidget(),
        DesktopWidgetKind.ScheduleBoard => new Widgets.ScheduleBoardWidget(),
        _ => new Widgets.CapsuleBarWidget(),
    };

    private void ApplyStyle(IDesktopWidget widget, DesktopWidgetKind kind)
    {
        var resolved = App.Settings.ResolveWidget(_settings.GetWidget(kind));

        if (_appliedStyles.TryGetValue(kind, out var previous) && previous == resolved)
        {
            return;
        }

        _appliedStyles[kind] = resolved;

        // 自绘控件连背景带圆角一起画在图层里，不存在窗口级背景、DWM 描边
        // 与区域裁剪这套问题，换主题只需换一套配色后重绘。
        if (widget is Widgets.WidgetWindow native)
        {
            native.ApplyTheme(resolved == VisualStyle.Light);
            return;
        }

        var window = (Window)widget;

        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = resolved == VisualStyle.Light ? ElementTheme.Light : ElementTheme.Dark;
        }

        // 不设置 SystemBackdrop：控件窗口会被裁成与卡片一致的圆角，
        // 任何窗口级背景都会从卡片圆角之外透出来，形成"双层窗口"的观感。

        Helpers.WindowHelper.ApplyAppIcon(window);

        // 用沉浸式深色模式让窗口的非客户区跟随主题，
        // 但**不能**调用 ApplyWindowChrome——它会把 DWM 圆角设回 8px，
        // 比卡片的 14/18px 小，于是卡片圆角被切掉、外面露出一圈直角背景。
        ThemeService.SetImmersiveDarkMode(window, resolved == VisualStyle.Light);

        // 隐藏 DWM 描边：否则深色卡片外面会挂一圈明显的高亮白边。
        ThemeService.SetBorderInvisible(window);

        // 关掉 DWM 自带圆角，改用与卡片完全一致的窗口区域裁剪。
        ThemeService.SetWindowCornerNone(window);
    }

    private void OnMainSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppSettings.MainStyle))
        {
            return;
        }

        // 设置变更在属性 setter 的调用链上，这里推迟到下一个调度周期，
        // 避免控件侧的任何问题反过来阻断主窗口的主题切换。
        var queue = _dispatcher ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue is null)
        {
            ApplyAll();
            return;
        }

        if (!queue.TryEnqueue(ApplyAll))
        {
            ApplyAll();
        }
    }
}
