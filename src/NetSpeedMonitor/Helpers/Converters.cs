using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NetSpeedMonitor.Helpers;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value as string ?? "#000000";
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class HexOpacityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value as string ?? "#000000";
        double opacity = 0.85;
        if (parameter is string ps && double.TryParse(ps, out var p)) opacity = p;
        else if (parameter is double pd) opacity = pd;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            color.A = (byte)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class OrientationToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var orientation = value as string ?? "Horizontal";
        var target = parameter as string ?? "Horizontal";
        return string.Equals(orientation, target, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percent = value is double d ? d : 0.0;
        double total = 60.0;
        if (parameter is string s && double.TryParse(s, out var parsed)) total = parsed;
        return Math.Max(0, Math.Min(total, percent / 100.0 * total));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public class DoubleToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double v = value is double d ? d : 0.0;
        int digits = 0;
        if (parameter is string s && int.TryParse(s, out var parsed)) digits = parsed;
        return v.ToString($"F{digits}", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
