using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetSpeedMonitor.Models;

public class AppConfig : INotifyPropertyChanged
{
    private string _theme = "Dark";
    private string _backgroundColor = "#24282E";
    private string _textColor = "#E8EAED";
    private string _uploadColor = "#7FD1C1";
    private string _downloadColor = "#6AA9F0";
    private string _memoryColor = "#B9C0C8";
    private double _opacity = 0.85;
    private double _fontSize = 13;
    private string _fontFamily = "Consolas";
    private double _cornerRadius = 8;
    private double _padding = 10;
    private bool _showUpload = true;
    private bool _showDownload = true;
    private bool _showMemory = true;
    private string _orientation = "Horizontal";
    private int _precision = 2;
    private int _updateIntervalMs = 1000;
    private bool _autoStart;
    private bool _snapToEdge = true;
    private bool _mousePassThrough;
    private bool _showMemoryBar = true;
    private bool _showMemoryPercent = true;
    private string _memoryBarColor = "#B9C0C8";
    private string _memoryBarBackground = "#55FFFFFF";
    private double _memoryBarWidth = 56;
    private double _memoryBarHeight = 6;
    private string _memoryBarPosition = "After";
    private string _memoryLabel = "M:";
    private string _adapterId = "Auto";
    private double _windowLeft = -99999;
    private double _windowTop = -99999;
    private bool _windowVisible = true;
    private string _trafficDate = "";
    private long _totalReceivedBytes;
    private long _totalSentBytes;
    private double _peakUpload;
    private double _peakDownload;

    public string Theme { get => _theme; set => Set(ref _theme, value); }
    public string BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, value); }
    public string TextColor { get => _textColor; set => Set(ref _textColor, value); }
    public string UploadColor { get => _uploadColor; set => Set(ref _uploadColor, value); }
    public string DownloadColor { get => _downloadColor; set => Set(ref _downloadColor, value); }
    public string MemoryColor { get => _memoryColor; set => Set(ref _memoryColor, value); }
    public double Opacity { get => _opacity; set => Set(ref _opacity, value); }
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }
    public string FontFamily { get => _fontFamily; set => Set(ref _fontFamily, value); }
    public double CornerRadius { get => _cornerRadius; set => Set(ref _cornerRadius, value); }
    public double Padding { get => _padding; set => Set(ref _padding, value); }
    public bool ShowUpload { get => _showUpload; set => Set(ref _showUpload, value); }
    public bool ShowDownload { get => _showDownload; set => Set(ref _showDownload, value); }
    public bool ShowMemory { get => _showMemory; set => Set(ref _showMemory, value); }
    public string Orientation { get => _orientation; set => Set(ref _orientation, value); }
    public int Precision { get => _precision; set => Set(ref _precision, value); }
    public int UpdateIntervalMs { get => _updateIntervalMs; set => Set(ref _updateIntervalMs, value); }
    public bool AutoStart { get => _autoStart; set => Set(ref _autoStart, value); }
    public bool SnapToEdge { get => _snapToEdge; set => Set(ref _snapToEdge, value); }

    /// <summary>鼠标穿透：所有鼠标事件直接落到窗口下方，避免遮挡其他窗口点击</summary>
    public bool MousePassThrough { get => _mousePassThrough; set => Set(ref _mousePassThrough, value); }

    /// <summary>是否显示内存进度条（与百分比文字独立控制）</summary>
    public bool ShowMemoryBar { get => _showMemoryBar; set => Set(ref _showMemoryBar, value); }
    /// <summary>是否显示内存百分比文字</summary>
    public bool ShowMemoryPercent { get => _showMemoryPercent; set => Set(ref _showMemoryPercent, value); }
    /// <summary>内存使用率前的标识文本（如 M: / MEM: / 内存:），留空则不显示</summary>
    public string MemoryLabel { get => _memoryLabel; set => Set(ref _memoryLabel, value); }
    /// <summary>进度条已用部分颜色</summary>
    public string MemoryBarColor { get => _memoryBarColor; set => Set(ref _memoryBarColor, value); }
    /// <summary>进度条底槽颜色</summary>
    public string MemoryBarBackground { get => _memoryBarBackground; set => Set(ref _memoryBarBackground, value); }
    /// <summary>进度条像素宽度</summary>
    public double MemoryBarWidth { get => _memoryBarWidth; set => Set(ref _memoryBarWidth, value); }
    /// <summary>进度条像素高度</summary>
    public double MemoryBarHeight { get => _memoryBarHeight; set => Set(ref _memoryBarHeight, value); }
    /// <summary>进度条相对百分比的位置：Before / After</summary>
    public string MemoryBarPosition { get => _memoryBarPosition; set => Set(ref _memoryBarPosition, value); }

    public string AdapterId { get => _adapterId; set => Set(ref _adapterId, value); }
    public double WindowLeft { get => _windowLeft; set => Set(ref _windowLeft, value); }
    public double WindowTop { get => _windowTop; set => Set(ref _windowTop, value); }
    public bool WindowVisible { get => _windowVisible; set => Set(ref _windowVisible, value); }
    public string TrafficDate { get => _trafficDate; set => Set(ref _trafficDate, value); }
    public long TotalReceivedBytes { get => _totalReceivedBytes; set => Set(ref _totalReceivedBytes, value); }
    public long TotalSentBytes { get => _totalSentBytes; set => Set(ref _totalSentBytes, value); }
    public double PeakUpload { get => _peakUpload; set => Set(ref _peakUpload, value); }
    public double PeakDownload { get => _peakDownload; set => Set(ref _peakDownload, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
