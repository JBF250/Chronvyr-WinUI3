using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using Chronvyr.Helpers;
using Chronvyr.Services;
using Chronvyr.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Chronvyr;

/// <summary>
/// 应用主窗口（FR-1、FR-8）。
/// 使用无边框窗口 + 自绘标题栏与窗口按钮，避免系统标题栏破坏整体视觉。
/// </summary>
public sealed partial class MainWindow : Window
{

    private bool _dragging;    private NativePoint _dragStartCursor;
    private PointInt32 _dragStartWindow;

    public MainWindow()
    {
        InitializeComponent();

        WindowHelper.ApplyDefaultSize(this);
        WindowHelper.ApplyAppIcon(this);
        SetupBorderlessWindow();

        // FR-8.4：关闭按钮仅隐藏窗口，进程后台常驻。
        AppWindow.Closing += OnAppWindowClosing;

        SetupTrayIcon();

        // 主界面每次到前台都把桌面控件压回它下面。
        // 控件窗口是独立顶层窗口，不这么做的话启动后它们会盖在主界面上。
        Activated += (_, _) => App.Widgets.SendWidgetsBehind();

        ApplyTheme();

        // 双保险：只要视觉风格设置发生变化就刷新窗口外观，
        // 不依赖调用方（设置页）记得调用 ApplyTheme。
        App.Settings.Current.PropertyChanged += OnSettingsChanged;

        AppWindow.Changed += (_, _) => UpdateMaximizeIcon();

        NavView.SelectedItem = NavView.MenuItems[0];
        NavigateTo("home");
        UpdateLocalizedChrome();
    }

    /// <summary>
    /// 去掉系统标题栏（保留窗口边框以便继续支持拖拽改变大小），
    /// 标题栏区域由 <c>AppTitleBar</c> 自绘。
    /// </summary>
    private void SetupBorderlessWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
        }

        // 边框线本身也隐藏，避免深色主题下出现一圈浅色描边。
        ThemeService.SetBorderInvisible(this);
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        var maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        MaximizeIcon.Glyph = maximized ? "\uE923" : "\uE922";
        ToolTipService.SetToolTip(MaximizeButton, maximized ? "还原" : "最大化");
    }

    /// <summary>
    /// 自绘标题栏的拖动。
    /// 早期版本用 <c>ReleaseCapture</c> + <c>WM_NCLBUTTONDOWN</c> 交给系统移动循环，
    /// 结果会卡在模态循环里（指针变等待状态、窗口黏住鼠标），因此改为自己跟踪指针位移。
    /// </summary>
    private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var point = e.GetCurrentPoint(null);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 最大化状态下先把窗口还原，再按还原后的位置继续拖动。
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized } presenter)
        {
            presenter.Restore();
            UpdateMaximizeIcon();
        }

        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        _dragging = true;
        _dragStartCursor = cursor;
        _dragStartWindow = AppWindow.Position;

        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnTitleBarPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || !GetCursorPos(out var cursor))
        {
            return;
        }

        AppWindow.Move(new PointInt32(
            _dragStartWindow.X + (cursor.X - _dragStartCursor.X),
            _dragStartWindow.Y + (cursor.Y - _dragStartCursor.Y)));
    }

    private void OnTitleBarPointerReleased(object sender, PointerRoutedEventArgs e) => EndDrag(sender, e);

    private void OnTitleBarPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndDrag(sender, e);

    private void EndDrag(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (presenter.State == OverlappedPresenterState.Maximized)
        {
            presenter.Restore();
        }
        else
        {
            presenter.Maximize();
        }

        UpdateMaximizeIcon();
    }

    /// <summary>关闭按钮 = 隐藏到托盘（FR-8.4）。</summary>
    private void OnCloseClick(object sender, RoutedEventArgs e) => WindowHelper.Hide(this);

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Models.AppSettings.MainStyle))
        {
            ApplyTheme();
        }
    }

    /// <summary>应用（或重新应用）当前视觉风格设置。</summary>
    public void ApplyTheme()
    {
        ThemeService.Apply(this, App.Settings.Current.MainStyle);
    }

    /// <summary>切换到指定导航标签对应的页面。</summary>
    public void NavigateTo(string tag)
    {
        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "calendar" => typeof(CalendarPage),
            "schedule" => typeof(SchedulePage),
            "diary" => typeof(DiaryPage),
            "pomodoro" => typeof(PomodoroPage),
            "workbench" => typeof(WorkbenchPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(HomePage),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        SelectNavItem(tag);
    }

    /// <summary>显示并激活主窗口（托盘菜单 / 双击托盘调用）。</summary>
    public void ShowFromTray()
    {
        WindowHelper.Show(this);
    }

    /// <summary>
    /// 重建当前页面。语言切换后调用，让所有 x:Bind 文本重新求值。
    /// </summary>
    public void ReloadCurrentPage()
    {
        var current = ContentFrame.CurrentSourcePageType;
        if (current is null)
        {
            return;
        }

        ContentFrame.Navigate(current);
        UpdateLocalizedChrome();
    }

    /// <summary>刷新窗口自身的本地化文本（导航项、标题栏提示、托盘）。</summary>
    public void UpdateLocalizedChrome()
    {
        var loc = App.Loc;

        ToolTipService.SetToolTip(MinimizeButton, loc["WindowMinimize"]);
        ToolTipService.SetToolTip(MaximizeButton, loc["WindowMaximize"]);
        ToolTipService.SetToolTip(CloseButton, loc["WindowClose"]);
        TrayIcon.ToolTipText = loc["AppName"];
        TitleBarText.Text = loc["AppName"];

        foreach (var item in NavView.MenuItems)
        {
            ApplyNavText(item, loc);
        }

        foreach (var item in NavView.FooterMenuItems)
        {
            ApplyNavText(item, loc);
        }
    }

    private static void ApplyNavText(object item, LocalizationService loc)
    {
        if (item is not NavigationViewItem { Tag: string tag } nav)
        {
            return;
        }

        nav.Content = tag switch
        {
            "home" => loc["NavHome"],
            "calendar" => loc["NavCalendar"],
            "schedule" => loc["NavSchedule"],
            "diary" => loc["NavDiary"],
            "pomodoro" => loc["NavPomodoro"],
            "workbench" => loc["NavWorkbench"],
            "settings" => loc["NavSettings"],
            _ => nav.Content,
        };
    }

    /// <summary>释放托盘图标。</summary>
    public void DisposeTray() => TrayIcon.Dispose();

    private void SetupTrayIcon()
    {
        // H.NotifyIcon 的 IconSource 会把图像按 ICO 解析，PNG 会导致
        // "Argument 'picture' must be a picture that can be used as a Icon"，
        // 因此这里直接使用 .ico 文件构造 System.Drawing.Icon。
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Chronvyr.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                TrayIcon.Icon = new System.Drawing.Icon(icoPath, 32, 32);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or OutOfMemoryException)
            {
                Log.WriteException("托盘图标加载失败", ex);
            }
        }

        TrayIcon.ToolTipText = "Chronvyr";

        // 交互约定（用户指定）：
        //   左键 / 双击 → 恢复主界面；右键 → 弹出菜单。
        // 托盘右键菜单走 WinUI 的 MenuFlyout（挂在一个隐身窗口上）：
        // 只有 XAML 的 MenuFlyoutPresenter 才带系统的 Desktop Acrylic 材质，
        // Win32 原生菜单无论怎么设置都拿不到。
        _trayFlyout = new TrayFlyoutWindow(
            onOpen: () =>
            {
                ShowFromTray();
                NavigateTo("home");
            },
            onSettings: () =>
            {
                ShowFromTray();
                NavigateTo("settings");
            },
            onExit: () =>
            {
                DisposeTray();
                App.Shutdown();
            });

        TrayIcon.RightClickCommand = new RelayCommand(() => _trayFlyout.ShowAtCursor());
        TrayIcon.DoubleClickCommand = new RelayCommand(ShowFromTray);
        TrayIcon.LeftClickCommand = new RelayCommand(ShowFromTray);
    }

    /// <summary>承载托盘菜单的隐身窗口（用 MenuFlyout 才能有亚克力）。</summary>
    private TrayFlyoutWindow? _trayFlyout;

    private void SelectNavItem(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && (string?)navItem.Tag == tag)
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }

        foreach (var item in NavView.FooterMenuItems)
        {
            if (item is NavigationViewItem navItem && (string?)navItem.Tag == tag)
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        WindowHelper.Hide(this);
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            NavigateTo(tag);
        }
    }
}
