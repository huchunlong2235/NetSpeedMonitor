using System.Net.NetworkInformation;

namespace NetSpeedMonitor.Services;

public class AdapterInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsVirtual { get; init; }
}

public class NetworkService
{
    private static readonly string[] VirtualKeywords =
    {
        "tap", "tun", "vgate", "clash", "mihomo", "virtual", "vmware", "virtualbox",
        "hyper-v", "vethernet", "wsl", "docker", "loopback", "wan miniport", "bluetooth",
        "vpn", "ras", "ndis", "lightweight filter",
        // NDIS 轻量过滤驱动会镜像物理网卡的字节计数，若不过滤会导致流量重复计数（速率翻倍）
        "qos packet scheduler", "packet scheduler", "wfp", "数据包计划程序"
    };

    private readonly Dictionary<string, (long Recv, long Sent)> _lastCounters = new();
    private bool _initialized;

    public List<AdapterInfo> GetAdapters()
    {
        var result = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var isVirtual = VirtualKeywords.Any(k => nic.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                                                     || nic.Description.Contains(k, StringComparison.OrdinalIgnoreCase));
            result.Add(new AdapterInfo
            {
                Id = nic.Id,
                Name = $"{nic.Name} ({nic.NetworkInterfaceType})",
                IsVirtual = isVirtual
            });
        }
        return result;
    }

    private IEnumerable<NetworkInterface> GetTargetAdapters(string adapterId)
    {
        var all = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        if (adapterId == "Auto")
            return all.Where(n => !IsVirtual(n));

        return all.Where(n => n.Id == adapterId);
    }

    private static bool IsVirtual(NetworkInterface nic)
        => VirtualKeywords.Any(k => nic.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                                    || nic.Description.Contains(k, StringComparison.OrdinalIgnoreCase));

    public (double Download, double Upload) Sample(string adapterId, double intervalSeconds)
    {
        var targets = GetTargetAdapters(adapterId).ToList();
        long recv = 0, sent = 0;
        var current = new Dictionary<string, (long Recv, long Sent)>();

        foreach (var nic in targets)
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                current[nic.Id] = (stats.BytesReceived, stats.BytesSent);
                recv += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch
            {
                // 网卡可能在采样瞬间被禁用，跳过
            }
        }

        if (!_initialized)
        {
            _initialized = true;
            _lastCounters.Clear();
            foreach (var kv in current) _lastCounters[kv.Key] = kv.Value;
            return (0, 0);
        }

        long lastRecv = 0, lastSent = 0;
        foreach (var kv in current)
        {
            if (_lastCounters.TryGetValue(kv.Key, out var last))
            {
                lastRecv += last.Recv;
                lastSent += last.Sent;
            }
        }

        // 网卡列表变化时（增删/重连）重新建立基线，避免峰值毛刺
        if (_lastCounters.Count != current.Count || current.Count == 0)
        {
            _lastCounters.Clear();
            foreach (var kv in current) _lastCounters[kv.Key] = kv.Value;
            return (0, 0);
        }

        _lastCounters.Clear();
        foreach (var kv in current) _lastCounters[kv.Key] = kv.Value;

        var dRecv = Math.Max(0, recv - lastRecv);
        var dSent = Math.Max(0, sent - lastSent);

        return (dRecv / intervalSeconds, dSent / intervalSeconds);
    }
}
