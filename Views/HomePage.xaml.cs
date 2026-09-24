using System.Globalization;
using Chronvyr.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Chronvyr.Views;

/// <summary>
/// 首页（FR-1.2）：欢迎词、当前时间日期、Windows 账户名称与头像。
/// </summary>
public sealed partial class HomePage : Page
{
    /// <summary>本地化服务（XAML 里以 <c>Loc.T('Key')</c> 取文本）。</summary>
    public Services.LocalizationService Loc => App.Loc;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public HomePage()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var name = await UserHelper.GetDisplayNameAsync();
        GreetingText.Text = App.Loc.Format(CurrentGreetingKey, name);

        // 微软账户头像优先；取不到就保留默认的用户图标。
        var picture = await UserHelper.GetAccountPictureAsync();
        if (picture is not null)
        {
            AvatarBrush.ImageSource = picture;
            AvatarClip.Visibility = Visibility.Visible;
            AvatarPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            AvatarClip.Visibility = Visibility.Collapsed;
            AvatarPlaceholder.Visibility = Visibility.Visible;
        }

        UpdateClock();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _timer.Stop();

    private void OnTick(object? sender, object e) => UpdateClock();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // 日期格式跟随界面语言，而不是固定中文格式。
        DateText.Text = now.ToString("D", CurrentCulture);
    }

    /// <summary>
    /// 按当前时段挑选问候语。
    /// 深夜与凌晨的文案带一点提醒（注意休息），白天则是常规问候。
    /// </summary>
    private static string CurrentGreetingKey => DateTime.Now.Hour switch
    {
        < 5 => "GreetingDawn",           // 00:00 - 04:59 夜深了
        < 8 => "GreetingEarlyMorning",   // 05:00 - 07:59 早晨
        < 11 => "GreetingMorning",       // 08:00 - 10:59 上午
        < 13 => "GreetingNoon",          // 11:00 - 12:59 中午
        < 17 => "GreetingAfternoon",     // 13:00 - 16:59 下午
        < 19 => "GreetingEvening",       // 17:00 - 18:59 傍晚
        < 23 => "GreetingNight",         // 19:00 - 22:59 晚上
        _ => "GreetingLateNight",        // 23:00 - 23:59 深夜
    };

    /// <summary>当前语言对应的日期格式文化。</summary>
    private static CultureInfo CurrentCulture => App.Loc.Language switch
    {
        Models.AppLanguage.ChineseTraditional => CultureInfo.GetCultureInfo("zh-TW"),
        Models.AppLanguage.English => CultureInfo.GetCultureInfo("en-US"),
        Models.AppLanguage.Japanese => CultureInfo.GetCultureInfo("ja-JP"),
        _ => CultureInfo.GetCultureInfo("zh-CN"),
    };
}
