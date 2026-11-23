namespace NetSpeedMonitor.Models;

public class ThemePreset
{
    public string Name { get; init; } = "";
    public string BackgroundColor { get; init; } = "";
    public string TextColor { get; init; } = "";
    public string UploadColor { get; init; } = "";
    public string DownloadColor { get; init; } = "";
    public string MemoryColor { get; init; } = "";
    public double Opacity { get; init; } = 0.85;

    public static IReadOnlyList<ThemePreset> All { get; } = new[]
    {
        new ThemePreset { Name = "深空灰", BackgroundColor = "#24282E", TextColor = "#E8EAED", UploadColor = "#7FD1C1", DownloadColor = "#6AA9F0", MemoryColor = "#B9C0C8", Opacity = 0.85 },
        new ThemePreset { Name = "极夜黑", BackgroundColor = "#000000", TextColor = "#FFFFFF", UploadColor = "#5DDCA5", DownloadColor = "#4FA3FF", MemoryColor = "#C8C8C8", Opacity = 0.75 },
        new ThemePreset { Name = "云端白", BackgroundColor = "#F5F6F7", TextColor = "#202124", UploadColor = "#0F9E6E", DownloadColor = "#185FA5", MemoryColor = "#5F5E5A", Opacity = 0.9 },
        new ThemePreset { Name = "深海蓝", BackgroundColor = "#12304F", TextColor = "#E6F1FB", UploadColor = "#5DCAA5", DownloadColor = "#85B7EB", MemoryColor = "#B5D4F4", Opacity = 0.85 },
        new ThemePreset { Name = "森林绿", BackgroundColor = "#17342B", TextColor = "#E1F5EE", UploadColor = "#97C459", DownloadColor = "#5DCAA5", MemoryColor = "#9FE1CB", Opacity = 0.85 },
        new ThemePreset { Name = "樱花粉", BackgroundColor = "#3A2530", TextColor = "#FBEAF0", UploadColor = "#ED93B1", DownloadColor = "#F4C0D1", MemoryColor = "#E5C9D6", Opacity = 0.85 },
    };

    public void ApplyTo(AppConfig config)
    {
        config.Theme = Name;
        config.BackgroundColor = BackgroundColor;
        config.TextColor = TextColor;
        config.UploadColor = UploadColor;
        config.DownloadColor = DownloadColor;
        config.MemoryColor = MemoryColor;
        config.Opacity = Opacity;
    }
}
