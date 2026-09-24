using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Chronvyr.Services;

/// <summary>硬件占用快照。</summary>
public readonly record struct HardwareSnapshot(
    double CpuUsage,
    double MemoryUsagePercent,
    double MemoryUsedGb,
    double MemoryTotalGb,
    double DiskUsagePercent,
    double GpuUsagePercent,
    bool HasBattery,
    int BatteryPercent,
    bool IsCharging);

/// <summary>
/// 硬件监控服务（FR-5.4 硬件监控卡片：硬件占用 + 电池详情）。
/// 直接使用 Win32 API，避免引入额外的硬件库依赖。
/// </summary>
public sealed class HardwareMonitorService
{
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrevious;

    /// <summary>读取一次硬件状态。</summary>
    public HardwareSnapshot Sample()
    {
        return new HardwareSnapshot(
            SampleCpu(),
            SampleMemory(out var usedGb, out var totalGb),
            usedGb,
            totalGb,
            SampleDisk(),
            SampleGpu(),
            SampleBattery(out var percent, out var charging),
            percent,
            charging);
    }

    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        var idleTicks = ToUInt64(idle);
        var kernelTicks = ToUInt64(kernel);
        var userTicks = ToUInt64(user);

        if (!_hasPrevious)
        {
            _prevIdle = idleTicks;
            _prevKernel = kernelTicks;
            _prevUser = userTicks;
            _hasPrevious = true;
            return 0;
        }

        var idleDelta = idleTicks - _prevIdle;
        var kernelDelta = kernelTicks - _prevKernel;
        var userDelta = userTicks - _prevUser;

        _prevIdle = idleTicks;
        _prevKernel = kernelTicks;
        _prevUser = userTicks;

        var total = kernelDelta + userDelta;
        if (total == 0)
        {
            return 0;
        }

        // kernel 时间已包含 idle，因此 busy = total - idle。
        var busy = total > idleDelta ? total - idleDelta : 0;
        return Math.Clamp(busy * 100d / total, 0, 100);
    }

    private static double SampleMemory(out double usedGb, out double totalGb)
    {
        usedGb = 0;
        totalGb = 0;

        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(ref status))
        {
            return 0;
        }

        const double gb = 1024d * 1024d * 1024d;
        totalGb = status.TotalPhys / gb;
        usedGb = (status.TotalPhys - status.AvailPhys) / gb;
        return status.MemoryLoad;
    }

    private static bool SampleBattery(out int percent, out bool charging)
    {
        percent = 0;
        charging = false;

        if (!GetSystemPowerStatus(out var status))
        {
            return false;
        }

        // 255 表示未知（通常是台式机没有电池）。
        if (status.BatteryLifePercent == 255)
        {
            return false;
        }

        percent = status.BatteryLifePercent;
        charging = status.ACLineStatus == 1;
        return true;
    }

    /// <summary>系统盘占用率。读不到就返回 0，不抛异常——它只是卡片上的一行字。</summary>
    private static double SampleDisk()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root))
            {
                return 0;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return 0;
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return Math.Clamp(used * 100d / drive.TotalSize, 0, 100);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0;
        }
    }

    /// <summary>
    /// 显卡占用率：对「GPU Engine」所有引擎实例的利用率求和。
    /// 这是任务管理器同款口径——单个实例只是一个引擎（3D / Copy / 解码…），
    /// 加起来才是整卡的忙闲程度。
    /// </summary>
    private double SampleGpu()
    {
        // GPU 计数器是「锦上添花」：驱动没提供这类计数器、或 PDH 调用出任何问题，
        // 都只让这一行不显示，绝不能把整个应用带崩。
        try
        {
            if (!EnsureGpuCounters() || _gpuCounters is not { Count: > 0 } counters)
            {
                return 0;
            }

            var collectStatus = PdhCollectQueryData(_gpuQuery);
            if (collectStatus != 0)
            {
                return 0;
            }

            double sum = 0;

            foreach (var counter in counters)
            {
                if (PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, out var value) == 0
                    && value.Double > 0)
                {
                    sum += value.Double;
                }
            }

            var result = Math.Clamp(sum, 0, 100);
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _gpuCounters = null;
            _gpuInitTried = true;
            return 0;
        }
    }

    /// <summary>
    /// 首次采样时把 GPU 计数器准备好：计数器路径要用通配符展开出每个引擎实例，
    /// 展开失败（驱动没提供这类计数器）就永久降级为不显示 GPU。
    /// </summary>
    private bool EnsureGpuCounters()
    {
        if (_gpuInitTried)
        {
            return _gpuCounters is { Count: > 0 };
        }

        _gpuInitTried = true;
        _gpuCounters = [];

        try
        {
            if (PdhOpenQuery(null, 0, out _gpuQuery) != 0)
            {
                return false;
            }

            const string wildcard = @"\GPU Engine(*)\Utilization Percentage";
            uint size = 0;

            // 第一次调用只为了问出需要多大的缓冲区，返回值必然是「数据不够」。
            _ = PdhExpandWildCardPath(null, wildcard, 0, ref size, 0);
            if (size == 0)
            {
                return false;
            }

            // 容量留一倍余量：两次调用之间引擎实例数可能变化。
            var capacity = ((int)size * 2) + 256;
            var raw = Marshal.AllocHGlobal(capacity * 2);

            try
            {
                var length = (uint)capacity;
                if (PdhExpandWildCardPath(null, wildcard, raw, ref length, 0) != 0)
                {
                    return false;
                }

                // 手动按 \0 切分多字符串（尾部是双 \0）。
                var text = Marshal.PtrToStringUni(raw, (int)length) ?? string.Empty;
                var paths = new List<string>();                var start = 0;

                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] != '\0')
                    {
                        continue;
                    }

                    if (i > start)
                    {
                        paths.Add(text[start..i]);
                    }

                    start = i + 1;
                }

                foreach (var path in paths)
                {
                    // 必须用 PdhAddCounter 而不是 PdhAddEnglishCounter：
                    // 展开出来的是【本地化】计数器名，而 PdhAddEnglishCounter 只认英文名。
                    if (PdhAddCounter(_gpuQuery, path, 0, out var counter) == 0)
                    {
                        _gpuCounters.Add(counter);
                    }
                }

                return _gpuCounters.Count > 0;
            }
            finally
            {
                Marshal.FreeHGlobal(raw);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _gpuCounters = null;
            return false;
        }
    }

    private static ulong ToUInt64(FileTime time) => ((ulong)time.HighDateTime << 32) | time.LowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus lpSystemPowerStatus);

    // ── 显卡占用：走 Windows 自带的性能计数器（pdh.dll），不引入额外依赖 ──

    private const uint PdhFmtDouble = 0x0000_0200;

    private nint _gpuQuery;
    private List<nint>? _gpuCounters;
    private bool _gpuInitTried;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    /// <summary>
    /// 展开通配符路径。输出是 PZZWSTR 多字符串，必须用原始缓冲区接收——
    /// 用 StringBuilder 的话 marshaller 按单字符串处理，遇到第一个 \0 就截断，
    /// 559 个引擎实例只会剩下 1 条。
    /// </summary>
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhExpandWildCardPath(
        string? dataSource, string wildcardPath, nint expanded, ref uint length, uint flags);

    /// <summary>用英文计数器名，避免中文系统上路径对不上。</summary>
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(nint query, string path, nint userData, out nint counter);

    /// <summary>按路径自身的语言添加计数器，与 PdhExpandWildCardPath 的展开结果配对。</summary>
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddCounter(nint query, string path, nint userData, out nint counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(nint query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out PdhCounterValue value);

    /// <summary>
    /// PDH_FMT_COUNTERVALUE：4 字节状态码 + 对齐后的联合体，总大小 16 字节。
    /// 这里必须按原生布局声明——只声明一个 double（8 字节）的话，
    /// PDH 会写越界 8 字节破坏托管堆，而且取到的值永远是 0。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PdhCounterValue
    {
        [FieldOffset(0)]
        public uint Status;

        [FieldOffset(8)]
        public double Double;
    }
}
