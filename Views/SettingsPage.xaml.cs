using System.Collections.Generic;
using Chronvyr.Models;
using Chronvyr.Services;
using Chronvyr.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Chronvyr.Views;

/// <summary>
/// 设置页（FR-7）：视觉风格、桌面控件、开机自启。
/// </summary>
public sealed partial class SettingsPage : Page
{
    private bool _initializing = true;

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel(App.Settings.Current);

        InitializeComponent();

        AutoStartSwitch.IsOn = AutoStartService.IsEnabled();
        ViewModel.AutoStartHint = AutoStartSwitch.IsOn
            ? App.Loc["SettingsAutoStartOn"]
            : App.Loc["SettingsAutoStartIdle"];

        // 单选按钮的可视树要等布局完成才可用，因此延迟到 Loaded 再回填当前风格与语言。
        Loaded += OnLoaded;
    }

    /// <summary>页面视图模型。</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>本地化服务（XAML 里以 <c>Loc[Key]</c> 取文本）。</summary>
    public LocalizationService Loc => App.Loc;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var current = (int)ViewModel.Settings.MainStyle;

        foreach (var radio in EnumerateStyleRadios(this))
        {
            if (radio.Tag is string tag && int.TryParse(tag, out var index) && index == current)
            {
                radio.IsChecked = true;
                break;
            }
        }

        switch (ViewModel.Settings.Language)
        {
            case AppLanguage.ChineseTraditional:
                LangZhHant.IsChecked = true;
                break;
            case AppLanguage.English:
                LangEn.IsChecked = true;
                break;
            case AppLanguage.Japanese:
                LangJa.IsChecked = true;
                break;
            default:
                LangZh.IsChecked = true;
                break;
        }

        _initializing = false;
    }

    /// <summary>切换界面语言（FR-1.5）。</summary>
    private void OnLanguageRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string tag } || !int.TryParse(tag, out var index))
        {
            return;
        }

        // 语言变化会触发主窗口重建当前页面，这里只负责写入设置。
        ViewModel.LanguageIndex = index;
    }

    /// <summary>切换视觉风格（FR-7.1），立即生效。</summary>
    private void OnStyleRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        if (sender is not RadioButton { Tag: string tag } || !int.TryParse(tag, out var index))
        {
            return;
        }

        var style = (VisualStyle)index;
        Log.Write($"设置：切换视觉风格 -> {style}");

        try
        {
            // 设置值会触发桌面控件同步（内部已捕获异常），之后再刷新主窗口外观。
            ViewModel.Settings.MainStyle = style;
            ViewModel.UpdateStyleHint();

            if (App.MainWindowInstance is { } window)
            {
                window.ApplyTheme();

                if (window.Content is FrameworkElement root)
                {
                    Log.Write($"设置：ApplyTheme 之后 RequestedTheme={root.RequestedTheme} ActualTheme={root.ActualTheme}");
                }
            }
            else
            {
                Log.Write("设置：MainWindowInstance 为 null，主界面未更新");
            }

            App.Widgets.ApplyAll();
        }
        catch (Exception ex)
        {
            Log.WriteException("设置：切换视觉风格失败", ex);
        }
    }

    /// <summary>开机自启（FR-7.3）。</summary>
    private void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var enabled = AutoStartSwitch.IsOn;
        var ok = AutoStartService.SetEnabled(enabled);
        ViewModel.Settings.AutoStart = enabled && ok;

        ViewModel.AutoStartHint = ok
            ? (enabled ? "已写入注册表，开机将自动启动。" : "已从注册表移除，开机不再自动启动。")
            : "写入注册表失败，可能被安全软件拦截。";
    }

    /// <summary>递归枚举页面上属于 MainStyle 分组的单选按钮。</summary>
    private static IEnumerable<RadioButton> EnumerateStyleRadios(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is RadioButton radio && radio.GroupName == "MainStyle")
            {
                yield return radio;
            }

            foreach (var nested in EnumerateStyleRadios(child))
            {
                yield return nested;
            }
        }
    }
}
