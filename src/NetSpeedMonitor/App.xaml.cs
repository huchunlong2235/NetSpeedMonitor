using System.IO;
using System.Windows;
using System.Windows.Forms;
using NetSpeedMonitor.Services;
using NetSpeedMonitor.ViewModels;
using NetSpeedMonitor.Views;
using Application = System.Windows.Application;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace NetSpeedMonitor;

public partial class App : Application
{
    private static Mutex? _mutex;
    private NotifyIcon? _trayIcon;
    private MainViewModel? _main;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settings;
    private HistoryWindow? _history;
    private ToastWindow? _toast;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception.ToString());
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogError(args.ExceptionObject?.ToString() ?? "未知异常");

        _mutex = new Mutex(true, "NetSpeedMonitor_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("程序已在运行，请查看系统托盘。", "网速监控", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var configService = new ConfigService();
        _main = new MainViewModel(configService);
        _main.RequestOpenSettings += OpenSettings;
        _main.RequestShowHistory += ShowHistory;
        _main.RequestExit += ExitApp;
        _main.RequestHideWindow += HideOverlay;
        _main.MemoryCleaned += ShowCleanToast;

        _overlay = new OverlayWindow(_main);
        if (_main.Config.WindowVisible) _overlay.Show();

        InitializeTray();

        // 支持命令行参数：--settings 直接打开设置，--history 直接打开历史曲线
        if (e.Args.Contains("--settings")) OpenSettings();
        if (e.Args.Contains("--history")) ShowHistory();
    }

    private void InitializeTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new MenuItem("显示 / 隐藏", null, (_, _) => ToggleOverlay()));
        menu.Items.Add(new MenuItem("历史曲线", null, (_, _) => ShowHistory()));
        var cleanItem = new MenuItem("清理内存", null, (_, _) => _main?.CleanMemory());
        menu.Opening += (_, _) =>
        {
            // 热键留空/无效时不显示括号，与热键注册逻辑（TryParse 失败不注册）保持一致
            var hk = _main?.Config.CleanHotkey;
            cleanItem.Text = NetSpeedMonitor.Helpers.GlobalHotkeys.TryParse(hk, out _, out _)
                ? $"清理内存 ({hk})"
                : "清理内存";
        };
        menu.Items.Add(cleanItem);
        menu.Items.Add(new MenuItem("设置", null, (_, _) => OpenSettings()));
        menu.Items.Add(new ToolStripSeparator());

        var passThroughItem = new MenuItem("鼠标穿透 (Ctrl+Alt+M)")
        {
            CheckOnClick = true,
            Checked = _main?.Config.MousePassThrough == true
        };
        passThroughItem.Click += (_, _) =>
        {
            if (_main is null) return;
            _main.Config.MousePassThrough = passThroughItem.Checked;
            _main.SaveConfig();
        };
        // 设置窗口/热键也可能改变状态，每次打开菜单时同步勾选
        menu.Opening += (_, _) => passThroughItem.Checked = _main?.Config.MousePassThrough == true;
        menu.Items.Add(passThroughItem);
        menu.Items.Add(new MenuItem("退出", null, (_, _) => ExitApp()));

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "网速监控 · 双击显示/隐藏",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ToggleOverlay();
    }

    /// <summary>从 exe 的 Win32 资源提取应用图标（ApplicationIcon 编译期嵌入）；失败回退系统图标</summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (ico is not null) return ico;
            }
        }
        catch { }
        return System.Drawing.SystemIcons.Information;
    }

    private void ToggleOverlay()
    {
        if (_overlay is null) return;
        if (_overlay.IsVisible)
            HideOverlay();
        else
            ShowOverlay();
    }

    private void ShowOverlay()
    {
        if (_overlay is null) return;
        _overlay.Show();
        _overlay.Activate();
        // 持久化"显示"状态，供下次开机自启恢复（此前只 Show 不落盘，导致状态永远回不到 true）
        if (_main is not null)
        {
            _main.Config.WindowVisible = true;
            _main.SaveConfig();
        }
    }

    private void HideOverlay()
    {
        _overlay?.Hide();
        if (_main is not null)
        {
            _main.Config.WindowVisible = false;
            _main.SaveConfig();
        }
    }

    private void OpenSettings()
    {
        if (_settings is null || !_settings.IsLoaded)
        {
            _settings = new SettingsWindow(new SettingsViewModel(_main!));
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        _settings.Activate();
    }

    private void ShowHistory()
    {
        if (_history is null || !_history.IsLoaded)
        {
            _history = new HistoryWindow(_main!);
            _history.Closed += (_, _) => _history = null;
        }
        _history.Show();
        _history.Activate();
    }

    private void ShowCleanToast(string message)
    {
        // 只用自绘 Toast：系统气泡与 Toast 同处屏幕右下角，会互相重叠
        void Show()
        {
            if (_toast is null)
            {
                _toast = new ToastWindow();
                _toast.Closed += (_, _) => _toast = null;
            }
            _toast.ShowMessage(message);
        }

        if (Dispatcher.CheckAccess()) Show();
        else Dispatcher.Invoke(Show);
    }

    private void ExitApp()
    {
        _main?.Shutdown();
        if (_toast is { IsVisible: true }) _toast.Close();
        if (_trayIcon is not null) _trayIcon.Visible = false;
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    internal static void LogDebug(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetSpeedMonitor");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "debug.log"), $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static void LogError(string message)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetSpeedMonitor");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
    }
}
