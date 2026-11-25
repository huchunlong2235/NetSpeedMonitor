using Microsoft.Win32;

namespace NetSpeedMonitor.Services;

public class AutoStartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NetSpeedMonitor";

    public void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key is null) return;
        if (enable)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    public bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) is not null;
    }

    /// <summary>
    /// 启动时自愈：注册表登记的是绝对路径，exe 移动/改名后登记项会失效
    /// （旧路径不存在，重启后程序不再自启，而设置里的勾选仍在）。
    /// 按当前配置意图校正：要求自启但路径与当前 exe 不符 → 重写；
    /// 要求关闭但注册表有残留（孤儿项）→ 清除。
    /// </summary>
    public void EnsureRegistered(bool wantAutoStart)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key is null) return;

        var existing = key.GetValue(AppName) as string;
        if (wantAutoStart)
        {
            if (existing is null
                || !string.Equals(existing.Trim('"'), exePath, StringComparison.OrdinalIgnoreCase))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else if (existing is not null)
        {
            key.DeleteValue(AppName, false);
        }
    }
}
