namespace Chronvyr.Widgets;

/// <summary>
/// 桌面控件清单：所有自绘控件在这里登记一次，其余模块（设置页、桌面控件管理器、
/// 主题切换）都从注册表读取。以后新增控件只需往这里追加一条，不必改动别处。
/// </summary>
public static class WidgetCatalog
{
    private static bool _initialized;

    /// <summary>登记全部控件，重复调用无副作用。</summary>
    public static void EnsureRegistered()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        WidgetRegistry.Register(new WidgetDescriptor
        {
            Id = "DiaryBar",
            NameKey = "WidgetDiaryBar",
            Create = static () => new DiaryBarWidget(),
            DefaultWidth = (int)DiaryBarWidget.CardWidth,
            DefaultHeight = (int)DiaryBarWidget.CardHeight,
            CollapsedHeight = (int)DiaryBarWidget.CollapsedCardHeight,
        });

        WidgetRegistry.Register(new WidgetDescriptor
        {
            Id = "ScheduleBoard",
            NameKey = "WidgetScheduleBoard",
            Create = static () => new ScheduleBoardWidget(),
            DefaultWidth = (int)ScheduleBoardWidget.CardWidth,
            DefaultHeight = (int)ScheduleBoardWidget.CardHeight,
            CollapsedHeight = (int)ScheduleBoardWidget.CollapsedCardHeight,
        });

        WidgetRegistry.Register(new WidgetDescriptor
        {
            Id = "DynamicIsland",
            NameKey = "WidgetDynamicIsland",
            Create = static () => new CapsuleBarWidget(),
            DefaultWidth = (int)CapsuleBarWidget.DefaultWidth,
            DefaultHeight = (int)CapsuleBarWidget.PanelHeight,

            // 胶囊栏自带「窄条 ⇄ 面板」的展开逻辑，不再叠加框架的折叠功能。
            AllowCollapse = false,
            CollapsedHeight = (int)CapsuleBarWidget.BarHeight,
        });
    }
}
