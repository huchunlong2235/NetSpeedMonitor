using System.Windows;
using System.Windows.Controls;
using NetSpeedMonitor.Models;
using NetSpeedMonitor.ViewModels;

namespace NetSpeedMonitor.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private Dictionary<string, object?> _snapshot = new();

    // 色卡 Tag -> 配置属性读写器。点击色卡弹系统选色器，选中后写回配置。
    private readonly Dictionary<string, (Func<string?> Get, Action<string> Set)> _colorProps;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        _colorProps = new Dictionary<string, (Func<string?>, Action<string>)>
        {
            ["BackgroundColor"]      = (() => _vm.Config.BackgroundColor,      v => _vm.Config.BackgroundColor = v),
            ["TextColor"]            = (() => _vm.Config.TextColor,            v => _vm.Config.TextColor = v),
            ["UploadColor"]          = (() => _vm.Config.UploadColor,          v => _vm.Config.UploadColor = v),
            ["DownloadColor"]        = (() => _vm.Config.DownloadColor,        v => _vm.Config.DownloadColor = v),
            ["MemoryColor"]          = (() => _vm.Config.MemoryColor,          v => _vm.Config.MemoryColor = v),
            ["MemoryBarColor"]       = (() => _vm.Config.MemoryBarColor,       v => _vm.Config.MemoryBarColor = v),
            ["MemoryBarBackground"]  = (() => _vm.Config.MemoryBarBackground,  v => _vm.Config.MemoryBarBackground = v),
        };

        Loaded += (_, _) =>
        {
            _snapshot = Snapshot(vm.Config);
            _vm.SelectedTheme = FindTheme(vm.Config.Theme);
            _vm.SelectedAdapter = FindAdapter(vm.Config.AdapterId);
        };
    }

    private NetSpeedMonitor.Models.ThemePreset? FindTheme(string name)
        => _vm.Themes.FirstOrDefault(t => t.Name == name);

    private NetSpeedMonitor.Services.AdapterInfo? FindAdapter(string id)
        => _vm.Adapters.FirstOrDefault(a => a.Id == id);

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string key
            || !_colorProps.TryGetValue(key, out var prop))
            return;

        string current = prop.Get() ?? string.Empty;
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = TryParseHex(current),
            FullOpen = true,
        };
        if (dialog.ShowDialog(new WindowHandle(this)) != System.Windows.Forms.DialogResult.OK)
            return;

        // 原色号若带 Alpha 通道（#AARRGGBB，9 位）则保留 Alpha 前缀，普通色写 #RRGGBB
        string prefix = current.Length == 9 ? current.Substring(0, 3) : "#";
        prop.Set($"{prefix}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }

    private static System.Drawing.Color TryParseHex(string hex)
    {
        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(hex)
                is System.Windows.Media.Color c)
                return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch
        {
            // 非法色号回退白色，选色器仍可正常打开
        }
        return System.Drawing.Color.White;
    }

    /// <summary>让 WinForms 选色器以设置窗口为宿主（模态归属）</summary>
    private sealed class WindowHandle : System.Windows.Forms.IWin32Window
    {
        private readonly Window _window;
        public WindowHandle(Window window) => _window = window;
        public IntPtr Handle => new System.Windows.Interop.WindowInteropHelper(_window).Handle;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // RadioButton 使用单向绑定，保存时根据界面选中项写回配置
        var orientationPanel = FindVisualChildren<System.Windows.Controls.RadioButton>(this).FirstOrDefault(r => r.GroupName == "Orientation" && r.IsChecked == true);
        if (orientationPanel is not null)
            _vm.Config.Orientation = orientationPanel.Content?.ToString() == "纵向排列" ? "Vertical" : "Horizontal";

        _vm.SaveCommand.Execute(null);
        // 窗口由 App 用 Show() 非模态打开，不能设置 DialogResult（会抛 InvalidOperationException 导致 Close 不执行）
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Restore(_vm.Config, _snapshot);
        _vm.SaveCommand.Execute(null);
        Close();
    }

    // 运行时动态字段：不是"设置"，取消时不应回退。
    // 否则设置窗口打开期间采样仍在累加，点取消会把统计/窗口位置倒退回打开瞬间的快照。
    private static readonly HashSet<string> RuntimeFields = new(StringComparer.Ordinal)
    {
        nameof(AppConfig.TrafficDate),
        nameof(AppConfig.TotalReceivedBytes),
        nameof(AppConfig.TotalSentBytes),
        nameof(AppConfig.PeakUpload),
        nameof(AppConfig.PeakDownload),
        nameof(AppConfig.WindowLeft),
        nameof(AppConfig.WindowTop),
        nameof(AppConfig.WindowVisible),
    };

    private static Dictionary<string, object?> Snapshot(AppConfig config)
    {
        return typeof(AppConfig)
            .GetProperties()
            .Where(p => p.CanRead && p.CanWrite && !RuntimeFields.Contains(p.Name))
            .ToDictionary(p => p.Name, p => p.GetValue(config));
    }

    private static void Restore(AppConfig config, Dictionary<string, object?> snapshot)
    {
        foreach (var prop in typeof(AppConfig).GetProperties())
        {
            if (prop.CanRead && prop.CanWrite && snapshot.TryGetValue(prop.Name, out var value))
                prop.SetValue(config, value);
        }
    }

    public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }
}
