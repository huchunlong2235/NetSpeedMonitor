using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NetSpeedMonitor.Helpers;
using NetSpeedMonitor.Models;
using NetSpeedMonitor.Services;

namespace NetSpeedMonitor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly AutoStartService _autoStartService = new();
    private readonly NetworkService _networkService = new();
    private readonly MemoryService _memoryService = new();
    private readonly HistoryService _historyService = new();
    private readonly MemoryCleanService _cleanService = new();
    private readonly DispatcherTimer _timer = new();
    private DateTime _lastCleanTime = DateTime.MinValue;
    private bool _thresholdTriggered; // 阈值触发防抖：低于阈值-5% 才复位

    private double _downloadSpeed;
    private double _uploadSpeed;
    private double _memoryUsage;
    private double _memoryBarFillWidth;
    private string _downloadText = "0 B/s";
    private string _uploadText = "0 B/s";
    private string _memoryText = "0%";
    private double _queueDownload;
    private double _queueUpload;
    private int _sampleCount;

    public AppConfig Config { get; }

    public MainViewModel(ConfigService configService)
    {
        _configService = configService;
        Config = configService.Load();

        // 当日流量：跨天自动清零
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (!string.Equals(Config.TrafficDate, today, StringComparison.Ordinal))
        {
            Config.TrafficDate = today;
            Config.TotalReceivedBytes = 0;
            Config.TotalSentBytes = 0;
            Config.PeakUpload = 0;
            Config.PeakDownload = 0;
        }

        Config.PropertyChanged += OnConfigChanged;

        // 自愈自启登记：exe 移动/改名后注册表指向旧路径会失效，按当前配置校正
        _autoStartService.EnsureRegistered(Config.AutoStart);

        _timer.Interval = TimeSpan.FromMilliseconds(Config.UpdateIntervalMs);
        _timer.Tick += OnTick;
        _timer.Start();

        SaveConfigCommand = new RelayCommand(SaveConfig);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ShowHistoryCommand = new RelayCommand(ShowHistory);
        ExitCommand = new RelayCommand(ExitApp);
        HideWindowCommand = new RelayCommand(HideWindow);
        TogglePassThroughCommand = new RelayCommand(() => Config.MousePassThrough = !Config.MousePassThrough);
        CleanMemoryCommand = new RelayCommand(CleanMemory);
    }

    public System.Windows.Media.Brush OverlayBackground
    {
        get
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Config.BackgroundColor);
                color.A = (byte)Math.Round(Math.Max(0, Math.Min(1, Config.Opacity)) * 255);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch { return System.Windows.Media.Brushes.Gray; }
        }
    }

    public double DownloadSpeed { get => _downloadSpeed; set => Set(ref _downloadSpeed, value); }
    public double UploadSpeed { get => _uploadSpeed; set => Set(ref _uploadSpeed, value); }
    public double MemoryUsage { get => _memoryUsage; set => Set(ref _memoryUsage, value); }
    /// <summary>进度条已填充部分的实际像素宽度（随内存占用率与配置宽度联动）</summary>
    public double MemoryBarFillWidth { get => _memoryBarFillWidth; private set => Set(ref _memoryBarFillWidth, value); }
    public string DownloadText { get => _downloadText; set => Set(ref _downloadText, value); }
    public string UploadText { get => _uploadText; set => Set(ref _uploadText, value); }
    public string MemoryText { get => _memoryText; set => Set(ref _memoryText, value); }
    public HistoryService History => _historyService;
    public List<AdapterInfo> Adapters => _networkService.GetAdapters();

    public ICommand SaveConfigCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ShowHistoryCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand HideWindowCommand { get; }
    public ICommand TogglePassThroughCommand { get; }
    public ICommand CleanMemoryCommand { get; }

    /// <summary>清理完成事件：参数为结果描述，供托盘气泡提示</summary>
    public event Action<string>? MemoryCleaned;

    public event Action? RequestOpenSettings;
    public event Action? RequestShowHistory;
    public event Action? RequestExit;
    public event Action? RequestHideWindow;

    private void OnTick(object? sender, EventArgs e)
    {
        var intervalSec = Math.Max(0.1, Config.UpdateIntervalMs / 1000.0);
        var (download, upload) = _networkService.Sample(Config.AdapterId, intervalSec);
        var memory = _memoryService.ReadUsagePercent();

        // 指数滑动平均，抑制数字跳动
        _queueDownload = _queueDownload * 0.45 + download * 0.55;
        _queueUpload = _queueUpload * 0.45 + upload * 0.55;
        _sampleCount++;

        DownloadSpeed = _queueDownload;
        UploadSpeed = _queueUpload;
        MemoryUsage = memory;
        UpdateMemoryBarFillWidth();

        DownloadText = FormatSpeed(_queueDownload);
        UploadText = FormatSpeed(_queueUpload);
        MemoryText = $"{Math.Round(memory):0}%";

        _historyService.Add(_queueDownload, _queueUpload);

        // 累计当日流量：rate × 间隔 = 实际字节增量（用原始采样值，不含平滑）
        Config.TotalReceivedBytes += (long)Math.Round(download * intervalSec);
        Config.TotalSentBytes += (long)Math.Round(upload * intervalSec);
        if (download > Config.PeakDownload) Config.PeakDownload = download;
        if (upload > Config.PeakUpload) Config.PeakUpload = upload;

        if (_sampleCount % 15 == 0) SaveConfig();

        CheckAutoClean(memory);
    }

    /// <summary>检查阈值/周期触发条件，异步执行清理避免阻塞 UI 采样</summary>
    private void CheckAutoClean(double memoryPercent)
    {
        // 阈值触发：达到阈值且未处于已触发状态；回落到阈值-5% 才允许下次触发（防抖）
        if (Config.AutoCleanEnabled)
        {
            if (memoryPercent >= Config.CleanThreshold && !_thresholdTriggered)
            {
                _thresholdTriggered = true;
                TriggerClean($"内存达到 {memoryPercent:0}%");
            }
            else if (memoryPercent < Config.CleanThreshold - 5)
            {
                _thresholdTriggered = false;
            }
        }

        // 周期触发：距上次清理超过设定分钟数
        if (Config.PeriodicCleanEnabled && Config.PeriodicCleanMinutes > 0)
        {
            if (DateTime.Now - _lastCleanTime >= TimeSpan.FromMinutes(Config.PeriodicCleanMinutes))
                TriggerClean($"每隔 {Config.PeriodicCleanMinutes} 分钟");
        }
    }

    private bool _cleanInProgress;
    private void TriggerClean(string reason)
    {
        if (_cleanInProgress) return;
        _cleanInProgress = true;
        _lastCleanTime = DateTime.Now;

        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var result = _cleanService.Clean();
                var freedMb = result.FreedBytes / 1024.0 / 1024.0;
                var msg = $"已清理 {result.ProcessCount} 个进程，释放约 {freedMb:0} MB（{reason}）";
                System.Windows.Application.Current?.Dispatcher.Invoke(() => MemoryCleaned?.Invoke(msg));
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => MemoryCleaned?.Invoke($"清理失败：{ex.Message}"));
            }
            finally
            {
                _cleanInProgress = false;
            }
        });
    }

    /// <summary>手动清理（托盘菜单/快捷键/设置窗口）</summary>
    public void CleanMemory() => TriggerClean("手动触发");

    private string FormatSpeed(double bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = bytesPerSecond;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value.ToString($"F{Config.Precision}")} {units[unit]}";
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Config.UpdateIntervalMs))
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, Config.UpdateIntervalMs));
        if (e.PropertyName is nameof(Config.BackgroundColor) or nameof(Config.Opacity))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayBackground)));
        if (e.PropertyName == nameof(Config.AutoStart))
            _autoStartService.SetAutoStart(Config.AutoStart);
        if (e.PropertyName == nameof(Config.MemoryBarWidth))
            UpdateMemoryBarFillWidth();
    }

    private void UpdateMemoryBarFillWidth()
        => MemoryBarFillWidth = Math.Max(0, Math.Min(Config.MemoryBarWidth,
            MemoryUsage / 100.0 * Math.Max(1, Config.MemoryBarWidth)));

    public void SaveConfig() => _configService.Save(Config);

    private void OpenSettings() => RequestOpenSettings?.Invoke();
    private void ShowHistory() => RequestShowHistory?.Invoke();
    private void ExitApp() => RequestExit?.Invoke();
    private void HideWindow() => RequestHideWindow?.Invoke();

    public void Shutdown()
    {
        _timer.Stop();
        // 不再强制写 WindowVisible=false：保持退出前的真实可见状态，
        // 否则显示着退出也会被记成隐藏，导致下次开机自启后悬浮窗不显示。
        SaveConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
