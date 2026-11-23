using System;
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
/// 全局热键 Ctrl+Alt+M：在鼠标穿透状态下也能切回可交互模式。
/// 穿透后窗口本身无法点击，必须提供一条外部恢复路径。
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_M = 0x4D;
    private const int HOTKEY_ID = 0x4E53; // "NS"

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private bool _registered;

    public event Action? HotkeyPressed;

    public GlobalHotkey(IntPtr hwnd) => _hwnd = hwnd;

    public bool Register()
    {
        if (_registered) return true;
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_M);
        return _registered;
    }

    public IntPtr OnMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
        HotkeyPressed = null;
    }
}
