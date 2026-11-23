using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NetSpeedMonitor.ViewModels;

namespace NetSpeedMonitor.Views;

public partial class HistoryWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _refreshTimer = new();

    public HistoryWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
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

        double max = Math.Max(1024, points.Max(p => Math.Max(p.Download, p.Upload)) * 1.15);

        // 网格线
        for (var i = 0; i < 3; i++)
        {
            var y = height / 3.0 * i;
            var line = i switch { 0 => GridLine0, 1 => GridLine1, _ => GridLine2 };
            line.X1 = 0; line.X2 = width; line.Y1 = y; line.Y2 = y;
        }

        var uploadPoints = new PointCollection();
        var downloadPoints = new PointCollection();
        for (var i = 0; i < points.Count; i++)
        {
            var x = i / (double)(points.Count - 1) * width;
            var yu = height - points[i].Upload / max * height;
            var yd = height - points[i].Download / max * height;
            uploadPoints.Add(new System.Windows.Point(x, yu));
            downloadPoints.Add(new System.Windows.Point(x, yd));
        }
        UploadLine.Points = uploadPoints;
        DownloadLine.Points = downloadPoints;

        ScaleLabel.Text = $"纵轴上限约 {FormatSpeed(max)} · 每秒采样一次 · 共 {points.Count} 个采样点";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = bytesPerSecond;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
