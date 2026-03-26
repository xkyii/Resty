using System.Globalization;
using Avalonia.Data.Converters;

namespace Kx.Resty.Converters;

public static class IntConverters
{
    public static readonly IndexToBoolConverter IndexToBool = new();
}

public class IndexToBoolConverter : IValueConverter
{
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
        return null;
    }
}