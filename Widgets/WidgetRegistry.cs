namespace Chronvyr.Widgets;

/// <summary>
/// 桌面控件描述符。新增一个控件只需在 <see cref="WidgetRegistry"/> 里登记一条，
/// 它就会自动获得位置记忆、主题跟随、折叠状态与显隐开关，不必再改动别处代码。
/// </summary>
public sealed class WidgetDescriptor
{
    /// <summary>稳定标识，同时用作设置项的键名，登记后不可再改名。</summary>
    public required string Id { get; init; }

    /// <summary>本地化名称键（交给 Loc.T 取词条）。</summary>
    public required string NameKey { get; init; }

    /// <summary>创建控件实例的工厂。</summary>
    public required Func<WidgetWindow> Create { get; init; }

    /// <summary>默认逻辑宽度。</summary>
    public int DefaultWidth { get; init; } = 260;

    /// <summary>默认逻辑高度。</summary>
    public int DefaultHeight { get; init; } = 320;

    /// <summary>是否支持折叠成一条标题栏。</summary>
    public bool AllowCollapse { get; init; } = true;

    /// <summary>折叠后的逻辑高度。</summary>
    public int CollapsedHeight { get; init; } = 46;
}

/// <summary>
/// 桌面控件注册表：自绘控件的唯一登记处。桌面控件管理器按这里的登记顺序
/// 创建、显隐与切换主题，设置页也照此顺序展示。
/// </summary>
public static class WidgetRegistry
{
    private static readonly List<WidgetDescriptor> Registered = [];

    private static readonly Dictionary<string, WidgetWindow> Live = new(StringComparer.Ordinal);

    /// <summary>已登记的控件，按登记顺序。</summary>
    public static IReadOnlyList<WidgetDescriptor> All => Registered;

    /// <summary>登记一个控件。重复登记同一标识会直接报错，避免静默覆盖。</summary>
    public static void Register(WidgetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        foreach (var existing in Registered)
        {
            if (existing.Id == descriptor.Id)
            {
                throw new InvalidOperationException($"桌面控件 {descriptor.Id} 已登记，不能重复登记。");
            }
        }

        Registered.Add(descriptor);
    }

    /// <summary>按标识查找已登记的控件描述符。</summary>
    public static WidgetDescriptor? Find(string id)
    {
        foreach (var descriptor in Registered)
        {
            if (descriptor.Id == id)
            {
                return descriptor;
            }
        }

        return null;
    }

    /// <summary>取当前存活的自绘控件实例；未创建时返回 null。</summary>
    public static T? Instance<T>(string id)
        where T : WidgetWindow
        => Live.TryGetValue(id, out var widget) ? widget as T : null;

    /// <summary>登记一个已创建的自绘控件实例。</summary>
    public static void Attach(string id, WidgetWindow widget) => Live[id] = widget;

    /// <summary>关闭并移除一个自绘控件实例。</summary>
    public static void Detach(string id)
    {
        if (Live.Remove(id, out var widget))
        {
            widget.Dispose();
        }
    }

    /// <summary>把所有存活控件的配色切到指定主题。</summary>
    public static void ApplyTheme(bool light)
    {
        foreach (var widget in Live.Values)
        {
            widget.ApplyTheme(light);
        }
    }

    /// <summary>关闭所有自绘控件（退出程序时调用）。</summary>
    public static void CloseAll()
    {
        foreach (var widget in Live.Values)
        {
            widget.Dispose();
        }

        Live.Clear();
    }
}
