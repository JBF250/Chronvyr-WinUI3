namespace Chronvyr.Widgets;

/// <summary>
/// 控件的弹簧动画。
/// </summary>
/// <remarks>
/// 曲线取自参考项目 NotchPeninsula，它对「Q 弹」手感的取舍值得照搬：
/// <code>1 − cos(2.65·t·2π)·e^(−10.8·t)</code>，时长 450ms，最大过冲约 15.9%。
///
/// 两个关键点：
/// <list type="number">
///   <item>它是<b>时间驱动</b>而非进度归一化——不能写成 <c>Ease(t/duration)</c>，
///         否则过冲会随之被压缩，弹簧感消失。</item>
///   <item>尺寸变化、位移、形态切换三种动画必须共用这一条曲线，
///         分别写三份的话，改一处忘一处就会出现「这个动画弹、那个不弹」。</item>
/// </list>
/// </remarks>
internal static class WidgetSpring
{
    private const double Frequency = 2.65;
    private const double Decay = 10.8;

    /// <summary>一次完整动画的时长（秒）。取到曲线基本归位（约 99.7%）的时刻。</summary>
    public const double DurationSeconds = 0.450;

    /// <summary>按已过去的秒数求插值比例（可能短暂超过 1，那正是过冲）。</summary>
    public static float Ease(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
        {
            return 0f;
        }

        if (elapsedSeconds >= DurationSeconds)
        {
            return 1f;
        }

        var value = 1.0 - (Math.Cos(Frequency * elapsedSeconds * 2.0 * Math.PI) * Math.Exp(-Decay * elapsedSeconds));
        return (float)value;
    }

    /// <summary>在 from 与 to 之间插值。</summary>
    public static float Lerp(float from, float to, double elapsedSeconds)
        => from + ((to - from) * Ease(elapsedSeconds));
}

/// <summary>带状态的弹簧动画：记录起点、目标与开始时间，供逐帧取用。</summary>
internal sealed class WidgetSpringValue
{
    private readonly Func<long> _clock;

    private float _from;
    private float _to;
    private long _startedAt;

    /// <param name="clock">毫秒时钟，一般传 <c>() =&gt; Environment.TickCount64</c>。</param>
    /// <param name="initial">初始值。</param>
    public WidgetSpringValue(Func<long> clock, float initial = 0f)
    {
        _clock = clock;
        _from = initial;
        _to = initial;
        _startedAt = long.MinValue;
    }

    /// <summary>当前值（按弹簧曲线求解）。</summary>
    public float Value
    {
        get
        {
            if (_startedAt == long.MinValue)
            {
                return _to;
            }

            var elapsed = (_clock() - _startedAt) / 1000.0;
            return elapsed >= WidgetSpring.DurationSeconds ? _to : WidgetSpring.Lerp(_from, _to, elapsed);
        }
    }

    /// <summary>是否仍在动画中。</summary>
    public bool IsAnimating => _startedAt != long.MinValue && (_clock() - _startedAt) / 1000.0 < WidgetSpring.DurationSeconds;

    /// <summary>设为目标值；从当前实际值起步，因此中途改向也不会跳变。</summary>
    public void To(float target)
    {
        _from = Value;
        _to = target;
        _startedAt = _clock();
    }

    /// <summary>立即归位到指定值（不播动画）。</summary>
    public void Reset(float value)
    {
        _from = value;
        _to = value;
        _startedAt = long.MinValue;
    }
}
