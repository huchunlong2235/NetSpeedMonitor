namespace NetSpeedMonitor.Services;

public record SpeedPoint(DateTime Time, double Download, double Upload);

public class HistoryService
{
    private readonly object _lock = new();
    private readonly List<SpeedPoint> _points = new();
    private const int MaxPoints = 300; // 5 分钟 @ 1s

    /// <summary>缓冲容量（秒），供图表按固定时间窗口定位曲线</summary>
    public const int WindowSeconds = MaxPoints;

    public void Add(double download, double upload)
    {
        lock (_lock)
        {
            _points.Add(new SpeedPoint(DateTime.Now, download, upload));
            while (_points.Count > MaxPoints) _points.RemoveAt(0);
        }
    }

    public List<SpeedPoint> Snapshot()
    {
        lock (_lock)
        {
            return new List<SpeedPoint>(_points);
        }
    }

    public void Clear()
    {
        lock (_lock) _points.Clear();
    }
}
