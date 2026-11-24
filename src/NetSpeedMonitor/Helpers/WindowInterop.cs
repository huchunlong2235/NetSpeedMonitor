using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NetSpeedMonitor.Helpers;

/// <summary>
/// 窗口扩展样式操作：鼠标穿透（WS_EX_TRANSPARENT）。
/// 用 Get/SetWindowLong 的 Ptr 版本以兼容 64 位进程。
/// </summary>
public static class WindowInterop
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static int GetStyle(IntPtr hWnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, index).ToInt32() : GetWindowLong32(hWnd, index);

    private static void SetStyle(IntPtr hWnd, int index, int style)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, index, new IntPtr(style));
        else SetWindowLong32(hWnd, index, style);
    }

    public static bool IsClickThrough(IntPtr hWnd)
        => hWnd != IntPtr.Zero && (GetStyle(hWnd, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0;

    /// <summary>开启后鼠标点击/悬停全部穿透到下层窗口</summary>
    public static void SetClickThrough(IntPtr hWnd, bool enable)
    {
        if (hWnd == IntPtr.Zero) return;
        var style = GetStyle(hWnd, GWL_EXSTYLE);
        var desired = enable ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        if (style == desired) return;
        SetStyle(hWnd, GWL_EXSTYLE, desired);
        // SWP_FRAMECHANGED 让扩展样式立即生效
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }
}

/// <summary>
/// 全局热键注册器（支持多个）。
/// - Ctrl+Alt+M：切换鼠标穿透（穿透后窗口无法点击，必须留键盘恢复路径）
/// - Ctrl+Alt+C（可配置）：手动清理内存
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class Registration
    {
        public int Id;
        public string Name = "";
        public uint Modifiers;
        public uint VirtualKey;
        public Action? Handler;
    }

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Registration> _regs = new();
    private int _nextId = 0x4E53; // "NS" 起始，避免与其他应用冲突

    public GlobalHotkeys(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>解析热键字符串（如 "Ctrl+Alt+C"），返回是否合法</summary>
    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                default:
                    if (!TryParseKey(p, out virtualKey)) return false;
                    break;
            }
        }
        return virtualKey != 0;
    }

    private static bool TryParseKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1)
        {
            if (char.IsLetterOrDigit(key[0]))
            {
                vk = (uint)char.ToUpperInvariant(key[0]);
                return true;
            }
            return false;
        }
        var k = key.ToUpperInvariant();
        if (k.StartsWith("F") && int.TryParse(k.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x6F + fn); // VK_F1 = 0x70
            return true;
        }
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["SPACE"] = 0x20, ["ENTER"] = 0x0D, ["TAB"] = 0x09, ["ESC"] = 0x1B,
            ["HOME"] = 0x24, ["END"] = 0x23, ["PGUP"] = 0x21, ["PGDN"] = 0x22,
            ["INSERT"] = 0x2D, ["DELETE"] = 0x2E,
        };
        return map.TryGetValue(k, out vk);
    }

    /// <summary>注册或更新一个热键。同名重复调用会先注销旧的再注册新的。</summary>
    public bool Register(string name, string hotkeyText, Action handler)
    {
        if (!TryParse(hotkeyText, out var mods, out var vk)) return false;
        Unregister(name);
        var id = _nextId++;
        bool ok = RegisterHotKey(_hwnd, id, mods | MOD_NOREPEAT, vk);
        if (!ok) return false;
        _regs[id] = new Registration { Id = id, Name = name, Modifiers = mods, VirtualKey = vk, Handler = handler };
        return true;
    }

    public void Unregister(string name)
    {
        foreach (var kv in _regs.ToArray())
        {
            if (!string.Equals(kv.Value.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            UnregisterHotKey(_hwnd, kv.Key);
            _regs.Remove(kv.Key);
        }
    }

    public IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _regs.TryGetValue(wParam.ToInt32(), out var reg))
        {
            reg.Handler?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _regs.Keys) UnregisterHotKey(_hwnd, id);
        _regs.Clear();
    }
}
