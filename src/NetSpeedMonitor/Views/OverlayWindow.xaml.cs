using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using NetSpeedMonitor.Helpers;
using NetSpeedMonitor.ViewModels;

namespace NetSpeedMonitor.Views;

public partial class OverlayWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _positionLoaded;
    private GlobalHotkeys? _hotkeys;

    public OverlayWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.Config.MousePassThrough))
                ApplyPassThrough();
        };
    }

    private bool _initialPositionApplied;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        // Ctrl+Alt+M：穿透状态下无法点击窗口，用全局热键切回可交互
        _hotkeys = new GlobalHotkeys(hwnd);
        _hotkeys.Register("PassThrough", "Ctrl+Alt+M",
            () => _vm.Config.MousePassThrough = !_vm.Config.MousePassThrough);
        // 内存清理热键（可配置）
        RegisterCleanHotkey();
        _vm.Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.Config.CleanHotkey)) RegisterCleanHotkey();
        };
        HwndSource.FromHwnd(hwnd)?.AddHook(_hotkeys.OnMessage);

        ApplyPassThrough();
    }

    private void RegisterCleanHotkey()
    {
        if (_hotkeys is null) return;
        _hotkeys.Unregister("CleanMemory");
        if (GlobalHotkeys.TryParse(_vm.Config.CleanHotkey, out _, out _))
            _hotkeys.Register("CleanMemory", _vm.Config.CleanHotkey, () => _vm.CleanMemory());
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        base.OnClosed(e);
    }

    private void ApplyPassThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            WindowInterop.SetClickThrough(hwnd, _vm.Config.MousePassThrough);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 尺寸在 SizeToContent 下需等到 ContentRendered 才稳定，此处不设置位置
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        if (_initialPositionApplied) return;
        _initialPositionApplied = true;

        var cfg = _vm.Config;
        var workArea = SystemParameters.WorkArea;

        if (cfg.WindowLeft > -90000 && cfg.WindowTop > -90000)
        {
            Left = cfg.WindowLeft;
            Top = cfg.WindowTop;
        }
        else
        {
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Top + 20;
        }

        // 纠正任何越界（含保存的位置指向已变化的屏幕）
        if (Left + ActualWidth > workArea.Right) Left = workArea.Right - ActualWidth - 10;
        if (Top + ActualHeight > workArea.Bottom) Top = workArea.Bottom - ActualHeight - 10;
        if (Left < workArea.Left) Left = workArea.Left;
        if (Top < workArea.Top) Top = workArea.Top;

        _positionLoaded = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // ContextMenu 由 XAML 自动处理
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            _vm.ShowHistoryCommand.Execute(null);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialPositionApplied || !IsLoaded) return;
        var workArea = SystemParameters.WorkArea;
        if (Left + ActualWidth > workArea.Right)
            Left = Math.Max(workArea.Left, workArea.Right - ActualWidth - 10);
        if (Top + ActualHeight > workArea.Bottom)
            Top = Math.Max(workArea.Top, workArea.Bottom - ActualHeight - 10);
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (!_positionLoaded || !IsLoaded) return;
        if (!_vm.Config.SnapToEdge) return;

        var workArea = SystemParameters.WorkArea;
        const int threshold = 12;
        if (Math.Abs(Left - workArea.Left) < threshold) Left = workArea.Left;
        else if (Math.Abs(Left + ActualWidth - workArea.Right) < threshold) Left = workArea.Right - ActualWidth;

        if (Math.Abs(Top - workArea.Top) < threshold) Top = workArea.Top;
        else if (Math.Abs(Top + ActualHeight - workArea.Bottom) < threshold) Top = workArea.Bottom - ActualHeight;

        _vm.Config.WindowLeft = Left;
        _vm.Config.WindowTop = Top;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _vm.Config.WindowVisible = false;
        _vm.SaveConfig();
    }
}
