using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kx.Resty.Converters;

public static class SelectionBrushConverters
{
    public static readonly SelectedBrushConverter SelectedBrush = new();
}

public class SelectedBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Avalonia.Application.Current is not { } app) return null;

        var isSelected = value is bool b && b;
        if (isSelected && app.TryGetResource("Brush.AccentSelected", app.ActualThemeVariant, out var res) && res is IBrush brush)
            return brush;

        return Avalonia.Media.Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
