using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kx.Resty.Converters;

public static class BrushConverters
{
    public static readonly MethodBrushConverter MethodBrush = new();
}

public class MethodBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string brushKey &&
            Avalonia.Application.Current is { } app &&
            app.TryGetResource(brushKey, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
            return brush;
        if (Avalonia.Application.Current is { } app2 &&
            app2.TryGetResource("Brush.FG1", app2.ActualThemeVariant, out var fallback) &&
            fallback is IBrush fallbackBrush)
            return fallbackBrush;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}