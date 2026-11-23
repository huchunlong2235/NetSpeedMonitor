using System.Windows;
using System.Windows.Controls;
using NetSpeedMonitor.Models;
using NetSpeedMonitor.ViewModels;

namespace NetSpeedMonitor.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private Dictionary<string, object?> _snapshot = new();

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // RadioButton 使用单向绑定，保存时根据界面选中项写回配置
        var orientationPanel = FindVisualChildren<System.Windows.Controls.RadioButton>(this).FirstOrDefault(r => r.GroupName == "Orientation" && r.IsChecked == true);
        if (orientationPanel is not null)
            _vm.Config.Orientation = orientationPanel.Content?.ToString() == "纵向排列" ? "Vertical" : "Horizontal";

        _vm.SaveCommand.Execute(null);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Restore(_vm.Config, _snapshot);
        _vm.SaveCommand.Execute(null);
        DialogResult = false;
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
