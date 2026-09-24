using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace Chronvyr.Services;

/// <summary>
/// 音频电平服务（FR-5.4 胶囊栏音频条）。
/// 用 1024 点 FFT 把环回捕获的信号分成 4 个频段（低音 → 高音），
/// 各自做包络平滑后交给界面，这样四个条会分别跟随低频鼓点与高频镲片，
/// 而不是像早期版本那样把同一个总峰值延迟铺开。
/// 长时间无音频时回落到系统音量。
/// </summary>
public sealed class AudioLevelService : IDisposable
{
    /// <summary>频段数量（与胶囊栏上的音频条数量一致）。</summary>
    public const int BandCount = 4;

    private const int FftLength = 1024;
    private const int FftLogN = 10;              // log2(1024)
    private const float AttackRate = 0.62f;      // 上升快，跟得住鼓点
    private const float ReleaseRate = 0.17f;     // 下降慢，看起来更顺滑
    private const float PeakDecay = 0.78f;
    private const float MinDb = -68f;            // 低于此分贝视为静音

    /// <summary>各频段的 bin 边界（1024 点 FFT，约 43Hz/bin）。</summary>
    private static readonly int[] BandEdges = [1, 4, 12, 40, 160];

    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(700);

    private readonly Complex[] _fftBuffer = new Complex[FftLength];
    private readonly float[] _bandPeaks = new float[BandCount];
    private readonly float[] _bands = new float[BandCount];

    private WasapiLoopbackCapture? _capture;
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private DateTime _lastDataAt = DateTime.MinValue;
    private int _fftPos;
    private bool _disposed;

    /// <summary>总体电平（0-1），取各频段的最大值。</summary>
    public float Level { get; private set; }

    /// <summary>系统主音量（0-1）。</summary>
    public float SystemVolume { get; private set; }

    /// <summary>四个频段的平滑电平（0-1），索引 0 为最低频。</summary>
    public IReadOnlyList<float> Bands => _bands;

    /// <summary>启动捕获。失败时静默降级（音频条将只显示系统音量）。</summary>
    public void Start()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            SystemVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;

            _capture = new WasapiLoopbackCapture(_device);
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NAudio.MmException or ArgumentException)
        {
            // 无音频设备或权限受限：音频条将只显示系统音量。
            StopCapture();
        }
    }

    /// <summary>读取一次当前电平（供 UI 定时器调用）。</summary>
    public float Sample()
    {
        _ = SampleBands();
        return Level;
    }

    /// <summary>
    /// 推进一帧包络并返回四个频段的当前显示值。
    /// 上升用快速率、下降用慢速率，是让波形「丝滑」的关键。
    /// </summary>
    public float[] SampleBands()
    {
        if (_disposed)
        {
            return _bands;
        }

        try
        {
            if (_device is not null)
            {
                SystemVolume = _device.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // 忽略：设备可能已被移除。
        }

        var silent = _capture is null || DateTime.UtcNow - _lastDataAt > SilenceTimeout;
        if (silent)
        {
            // 没有音频时四条一起回落到系统音量，形成一条平稳的音量指示。
            for (var i = 0; i < BandCount; i++)
            {
                _bandPeaks[i] = SystemVolume;
            }
        }

        for (var i = 0; i < BandCount; i++)
        {
            var target = silent ? SystemVolume : _bandPeaks[i];
            var rate = target > _bands[i] ? AttackRate : ReleaseRate;
            _bands[i] += (target - _bands[i]) * rate;

            if (!silent)
            {
                _bandPeaks[i] *= PeakDecay;
            }
        }

        var max = 0f;
        foreach (var b in _bands)
        {
            if (b > max)
            {
                max = b;
            }
        }

        Level = max;
        return _bands;
    }

    /// <summary>停止捕获。</summary>
    public void Stop() => StopCapture();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopCapture();
        _device?.Dispose();
        _enumerator?.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = e.Buffer;

        // WASAPI 环回捕获默认是 32 位浮点采样。
        for (var i = 0; i + 4 <= e.BytesRecorded; i += 4)
        {
            var sample = BitConverter.ToSingle(buffer, i);

            // 加汉明窗，减少频谱泄漏。
            _fftBuffer[_fftPos].X = (float)(sample * FastFourierTransform.HammingWindow(_fftPos, FftLength));
            _fftBuffer[_fftPos].Y = 0f;

            _fftPos++;
            if (_fftPos < FftLength)
            {
                continue;
            }

            _fftPos = 0;
            FastFourierTransform.FFT(true, FftLogN, _fftBuffer);
            AccumulateBands();
        }

        _lastDataAt = DateTime.UtcNow;
    }

    /// <summary>把一帧频谱折算成四个频段的电平（分贝域归一化）。</summary>
    private void AccumulateBands()
    {
        for (var band = 0; band < BandCount; band++)
        {
            var from = BandEdges[band];
            var to = Math.Min(BandEdges[band + 1], FftLength / 2);

            var sum = 0f;
            var count = 0;
            for (var k = from; k < to; k++)
            {
                var re = _fftBuffer[k].X;
                var im = _fftBuffer[k].Y;
                sum += MathF.Sqrt((re * re) + (im * im));
                count++;
            }

            if (count == 0)
            {
                continue;
            }

            // 转成分贝再归一化：不同频段的绝对能量差异很大，
            // 直接线性比较会让低频永远压住高频。
            var amplitude = sum / count;
            var db = 20f * MathF.Log10(amplitude + 1e-7f);
            var normalized = Math.Clamp((db - MinDb) / -MinDb, 0f, 1f);

            if (normalized > _bandPeaks[band])
            {
                _bandPeaks[band] = normalized;
            }
        }
    }

    private void StopCapture()
    {
        if (_capture is null)
        {
            return;
        }

        try
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.StopRecording();
            _capture.Dispose();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NAudio.MmException)
        {
            // 忽略停止过程中的异常。
        }

        _capture = null;
    }
}
