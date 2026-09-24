using Chronvyr.Services;
using Microsoft.UI.Xaml;

namespace Chronvyr;

/// <summary>
/// 应用程序入口，负责创建并持有主窗口与各全局服务。
/// </summary>
public partial class App : Application
{
    /// <summary>主窗口实例。主窗口关闭时只隐藏，进程后台常驻（FR-8.4）。</summary>
    public static MainWindow? MainWindowInstance { get; private set; }

    /// <summary>全局数据仓储。</summary>
    public static DataService Data { get; private set; } = null!;

    /// <summary>全局设置服务。</summary>
    public static SettingsService Settings { get; private set; } = null!;

    /// <summary>全局番茄钟服务（主界面与胶囊栏共享）。</summary>
    public static PomodoroService Pomodoro { get; private set; } = null!;

    /// <summary>桌面控件管理器。</summary>
    public static DesktopWidgetManager Widgets { get; private set; } = null!;

    /// <summary>多语言服务（FR-1.5）。</summary>
    public static LocalizationService Loc { get; private set; } = null!;

    /// <summary>音频电平服务（胶囊栏音频条）。</summary>
    public static AudioLevelService Audio { get; private set; } = null!;

    /// <summary>硬件监控服务。</summary>
    public static HardwareMonitorService Hardware { get; private set; } = null!;

    /// <summary>天气服务。</summary>
    public static WeatherService Weather { get; private set; } = null!;

    /// <summary>剪切板历史服务。</summary>
    public static ClipboardService ClipboardHistory { get; private set; } = null!;

    public App()
    {
        // 单实例：多个进程同时写 JSON 数据文件会互相覆盖，因此只允许一个实例。
        if (!EnsureSingleInstance())
        {
            Environment.Exit(0);
            return;
        }

        InitializeComponent();

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.WriteException("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.WriteException("UnobservedTask", e.Exception);
            e.SetObserved();
        };
    }

    private static Mutex? _singleInstanceMutex;

    private static bool EnsureSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\Chronvyr.SingleInstance", out var createdNew);
            if (createdNew)
            {
                return true;
            }

            // 已有实例：把它的主窗口拉到前台，然后退出本次启动。
            Helpers.WindowHelper.TryActivateExistingWindow("Chronvyr");
            return false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            // 无法创建互斥体（极少见）：按允许多实例处理，不阻塞启动。
            return true;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 每次启动重置日志，便于定位本次运行的问题。
        Log.Write("=== Chronvyr 启动 ===");

        try
        {
            AppPaths.EnsureCreated();

            Data = new DataService();
            Log.Write("数据层就绪");

            // 首次运行（没有 settings.json）时按系统语言决定界面语言。
            var firstRun = !File.Exists(AppPaths.SettingsFile);
            Settings = new SettingsService();
            if (firstRun)
            {
                Settings.Current.Language = LocalizationService.DetectSystemLanguage();
            }

            Loc = new LocalizationService { Language = Settings.Current.Language };
            Loc.LanguageChanged += (_, _) =>
            {
                Settings.Current.Language = Loc.Language;
                MainWindowInstance?.ReloadCurrentPage();
            };

            Pomodoro = new PomodoroService();
            Audio = new AudioLevelService();
            Hardware = new HardwareMonitorService();
            Weather = new WeatherService();
            ClipboardHistory = new ClipboardService();
            Widgets = new DesktopWidgetManager(Settings.Current);
            Log.Write("服务就绪");

            MainWindowInstance = new MainWindow();
            MainWindowInstance.Activate();
            Log.Write("主窗口已激活");

            Chronvyr.Widgets.WidgetCatalog.EnsureRegistered();
            Widgets.ApplyAll();
            Log.Write("桌面控件已应用");
        }
        catch (Exception ex)
        {
            Log.WriteException("OnLaunched", ex);
            throw;
        }
    }

    /// <summary>退出应用前把数据落盘（托盘「退出 Chronvyr」调用）。</summary>
    public static void Shutdown()
    {
        Log.Write("=== Chronvyr 退出 ===");

        try
        {
            Data.Flush();
            Settings.Save();
            Audio.Dispose();
            ClipboardHistory.Stop();
        }
        catch (Exception ex)
        {
            Log.WriteException("退出清理失败", ex);
        }

        // 不能用 Application.Current.Exit()：主窗口只是隐藏（未关闭）时它不保证终止进程，
        // 会出现「托盘图标消失但进程仍在」的情况。这里直接结束进程。
        Environment.Exit(0);
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.WriteException("XamlUnhandled", e.Exception);

        // 记录后吞掉异常，避免出现 Win32 崩溃（NFR-1.1）。
        e.Handled = true;
    }
}
