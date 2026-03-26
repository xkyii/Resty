using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kx.Resty.Views
{
    /// <summary>Converts HttpMethodOption.BrushKey to an IBrush via App resources.</summary>
    public class MethodBrushConverter : IValueConverter
    {
        public static readonly MethodBrushConverter Instance = new();

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

    /// <summary>Converts a selected tab index to bool for RadioButton.IsChecked.
    /// ConverterParameter = expected tab index (string).</summary>
    public class IndexToBoolConverter : IValueConverter
    {
        public static readonly IndexToBoolConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int idx && parameter is string paramStr && int.TryParse(paramStr, out int expected))
                return idx == expected;
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is string paramStr && int.TryParse(paramStr, out int expected))
                return expected;
            return null; // don't update when unchecking
        }
    }
}
