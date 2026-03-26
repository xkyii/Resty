using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace Kx.Resty.Converters
{
    public static class StringConverters
    {
        public static readonly ToLocaleConverter ToLocale = new();
        public static readonly ToThemeConverter ToTheme = new();
    }

    public class ToLocaleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => Locale.Supported.Find(x => x.Key == value as string);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value as Locale)?.Key;
    }

    public class ToThemeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (s.Equals("Light", StringComparison.OrdinalIgnoreCase)) return ThemeVariant.Light;
                if (s.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return ThemeVariant.Dark;
            }
            return ThemeVariant.Default;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value as ThemeVariant)?.Key?.ToString() ?? "Default";
    }
}
