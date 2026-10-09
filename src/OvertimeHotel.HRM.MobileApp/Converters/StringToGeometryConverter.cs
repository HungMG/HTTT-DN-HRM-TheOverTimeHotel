using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace OvertimeHotel.HRM.MobileApp.Converters;

public class StringToGeometryConverter : IValueConverter
{
    private static readonly PathGeometryConverter s_converter = new();
    private static readonly ConcurrentDictionary<string, Geometry> s_cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string pathString && !string.IsNullOrWhiteSpace(pathString))
        {
            return s_cache.GetOrAdd(pathString, path => (Geometry)s_converter.ConvertFromInvariantString(path)!);
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
