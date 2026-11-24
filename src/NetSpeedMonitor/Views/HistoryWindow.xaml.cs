using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetSpeedMonitor.Services;
using NetSpeedMonitor.ViewModels;

namespace NetSpeedMonitor.Views;

public partial class HistoryWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly TextBlock[] _yLabels;
    private readonly Line[] _gridLines;
    private readonly TextBlock[] _xLabels;

    public HistoryWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _gridLines = new[] { GridLine0, GridLine1, GridLine2, GridLine3 };
        _yLabels = new[] { YLabel0, YLabel1, YLabel2, YLabel3 };
        _xLabels = new[] { XLabel0, XLabel1, XLabel2 };
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += (_, _) => Render();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Render();
        _refreshTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        var points = _vm.History.Snapshot();
        if (points.Count < 2)
        {
            EmptyHint.Visibility = Visibility.Visible;
            UploadLine.Points.Clear();
            DownloadLine.Points.Clear();
            return;
        }
        EmptyHint.Visibility = Visibility.Collapsed;

        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double peak = points.Max(p => Math.Max(p.Download, p.Upload));
        double max = NiceCeiling(Math.Max(1024, peak * 1.05));

        // 网格线 + Y 轴速度刻度（4 档：max, 2/3max, 1/3max, 0）
        for (var i = 0; i < 4; i++)
        {
            double y = height / 3.0 * i;
            var line = _gridLines[i];
            line.X1 = 0; line.X2 = width; line.Y1 = y; line.Y2 = y;

            var label = _yLabels[i];
            label.Text = FormatSpeed(max * (3 - i) / 3.0);
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, Math.Max(0, Math.Min(height - 13, y - 7)));
        }

        // 固定 5 分钟滚动窗口：终点 = 最新样本，起点 = 终点 - 窗口时长。
        // 曲线按时间戳定位（而非索引均分），数据不足时从右侧逐渐填充，
        // 起点时间随每秒采样持续前移，与任务管理器行为一致。
        var end = points[^1].Time;
        var start = end.AddSeconds(-HistoryService.WindowSeconds + 1);
        double spanSeconds = HistoryService.WindowSeconds - 1;
        double MapX(DateTime t) =>
            Math.Max(0, Math.Min(width, (t - start).TotalSeconds / spanSeconds * width));

        // 曲线
        var uploadPoints = new PointCollection();
        var downloadPoints = new PointCollection();
        foreach (var p in points)
        {
            double x = MapX(p.Time);
            uploadPoints.Add(new System.Windows.Point(x, height - p.Upload / max * height));
            downloadPoints.Add(new System.Windows.Point(x, height - p.Download / max * height));
        }
        UploadLine.Points = uploadPoints;
        DownloadLine.Points = downloadPoints;

        // X 轴时间刻度：起点 / 中点 / 终点（均按时间窗口计算，每秒刷新）
        var times = new[] { start, start.AddSeconds(spanSeconds / 2), end };
        for (var i = 0; i < 3; i++)
        {
            _xLabels[i].Text = times[i].ToString("HH:mm:ss");
            Canvas.SetTop(_xLabels[i], 2);
        }
        Canvas.SetLeft(_xLabels[0], 0);
        Canvas.SetLeft(_xLabels[1], width / 2 - 55);
        Canvas.SetLeft(_xLabels[2], width - 110);

        ScaleLabel.Text = $"纵轴上限 {FormatSpeed(max)} · 每秒采样一次 · 共 {points.Count} 个采样点";
    }

    /// <summary>取 1/2/5×10ⁿ 的整齐上限，使刻度值为整数档，视觉更稳定</summary>
    private static double NiceCeiling(double value)
    {
        if (value <= 0) return 1024;
        double exp = Math.Floor(Math.Log10(value));
        double pow = Math.Pow(10, exp);
        double frac = value / pow;
        double nice = frac <= 1 ? 1 : frac <= 2 ? 2 : frac <= 5 ? 5 : 10;
        return nice * pow;
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "0";
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = bytesPerSecond;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }
}
