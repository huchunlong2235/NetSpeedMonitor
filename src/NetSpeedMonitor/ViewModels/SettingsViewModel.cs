using System.ComponentModel;
using System.Windows.Input;
using NetSpeedMonitor.Helpers;
using NetSpeedMonitor.Models;
using NetSpeedMonitor.Services;

namespace NetSpeedMonitor.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _main;
    private readonly HistoryService _history;

    public AppConfig Config => _main.Config;
    public IReadOnlyList<ThemePreset> Themes => ThemePreset.All;
    public List<AdapterInfo> Adapters { get; }

    private static readonly AdapterInfo AutoAdapter = new()
    {
        Id = "Auto",
        Name = "自动（推荐 · 排除虚拟网卡）",
        IsVirtual = false
    };

    public string TodayDownloadText => FormatBytes(Config.TotalReceivedBytes);
    public string TodayUploadText => FormatBytes(Config.TotalSentBytes);
    public string PeakDownloadText => FormatSpeed(Config.PeakDownload);
    public string PeakUploadText => FormatSpeed(Config.PeakUpload);

    public ICommand ApplyThemeCommand { get; }
    public ICommand ResetTrafficCommand { get; }
    public ICommand SaveCommand { get; }

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        _history = main.History;
        Adapters = new List<AdapterInfo> { AutoAdapter };
        Adapters.AddRange(_main.Adapters);
        ApplyThemeCommand = new RelayCommand(ApplyTheme);
        ResetTrafficCommand = new RelayCommand(ResetTraffic);
        SaveCommand = new RelayCommand(Save);
        Config.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(Config.TotalReceivedBytes) or nameof(Config.TotalSentBytes)
                or nameof(Config.PeakDownload) or nameof(Config.PeakUpload))
            {
                OnPropertyChanged(nameof(TodayDownloadText));
                OnPropertyChanged(nameof(TodayUploadText));
                OnPropertyChanged(nameof(PeakDownloadText));
                OnPropertyChanged(nameof(PeakUploadText));
            }
        };
    }

    private ThemePreset? _selectedTheme;
    public ThemePreset? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged();
            if (value is not null) value.ApplyTo(Config);
        }
    }

    private AdapterInfo? _selectedAdapter;
    public AdapterInfo? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (_selectedAdapter == value) return;
            _selectedAdapter = value;
            OnPropertyChanged();
            if (value is not null) Config.AdapterId = value.Id;
        }
    }

    private void ApplyTheme()
    {
        if (SelectedTheme is not null) SelectedTheme.ApplyTo(Config);
    }

    private void ResetTraffic()
    {
        Config.TotalReceivedBytes = 0;
        Config.TotalSentBytes = 0;
        Config.PeakDownload = 0;
        Config.PeakUpload = 0;
        _history.Clear();
        _main.SaveConfig();
        OnPropertyChanged(nameof(TodayDownloadText));
        OnPropertyChanged(nameof(TodayUploadText));
        OnPropertyChanged(nameof(PeakDownloadText));
        OnPropertyChanged(nameof(PeakUploadText));
    }

    private void Save() => _main.SaveConfig();

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = bytesPerSecond;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
