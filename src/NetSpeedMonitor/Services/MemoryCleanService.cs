using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetSpeedMonitor.Services;

/// <summary>
/// 保守型内存清理：对每个用户进程调用 EmptyWorkingSet，把工作集页 trim 到备用列表。
/// 不碰系统级内存列表（standby/modified/file cache），因此不需要管理员权限、无未文档化 API。
/// 注意：这不是"增加物理内存"，只是把 Working Set 计数降下来，让任务管理器数字回落。
/// </summary>
public class MemoryCleanService
{
    private const int PROCESS_SET_QUOTA = 0x0100;
    private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // 系统关键进程：跳过，清理它们无意义且可能触发保护机制
    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "registry", "smss", "csrss", "wininit",
        "winlogon", "services", "lsass", "svchost", "fontdrvhost", "dwm",
        "sihost", "ctfmon", "audiodg", "applicationframehost", "shellexperiencehost",
        "searchindexer", "searchhost", "runtimebroker", "memorycompression",
        "secure system", "idle"
    };

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    private static long AvailablePhysicalBytes()
    {
        var m = new MEMORYSTATUSEX();
        m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        return GlobalMemoryStatusEx(ref m) ? (long)m.ullAvailPhys : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public sealed class CleanResult
    {
        public long FreedBytes { get; init; }
        public int ProcessCount { get; init; }
        public int SkippedCount { get; init; }
        public int FailedCount { get; init; }
        public DateTime Time { get; init; } = DateTime.Now;
    }

    /// <summary>执行一次清理，返回释放量统计（实际 WS 差值）</summary>
    public CleanResult Clean()
    {
        long freed = 0;
        int cleaned = 0, skipped = 0, failed = 0;
        int selfId = Environment.ProcessId;
        long availBefore = AvailablePhysicalBytes();

        var processes = Process.GetProcesses();
        try
        {
            foreach (var p in processes)
            {
                try
                {
                    if (p.Id == selfId) { skipped++; continue; }
                    if (CriticalProcesses.Contains(p.ProcessName)) { skipped++; continue; }

                    long before;
                    try { before = p.WorkingSet64; }
                    catch { skipped++; continue; }
                    if (before <= 0) { skipped++; continue; }

                    IntPtr handle = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                    if (handle == IntPtr.Zero) { failed++; continue; }

                    try
                    {
                        if (EmptyWorkingSet(handle))
                        {
                            // 不做逐进程 WS 差值统计：清理后进程可能立即重新分配，差值高估严重。
                            // 总释放量在全部清理完成后用系统可用物理内存差值统一计算。
                            cleaned++;
                        }
                        else failed++;
                    }
                    finally { CloseHandle(handle); }
                }
                catch { failed++; }
            }
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }

        // 等待系统内存统计刷新，再取可用物理内存差值作为真实释放量
        System.Threading.Thread.Sleep(100);
        long availAfter = AvailablePhysicalBytes();
        freed = Math.Max(0, availAfter - availBefore);

        return new CleanResult
        {
            FreedBytes = freed,
            ProcessCount = cleaned,
            SkippedCount = skipped,
            FailedCount = failed
        };
    }
}
