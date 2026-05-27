using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace FlexFamilyCalendar.Converters;

/// <summary>true → Rahmen (Standard 2px), false → kein Rahmen. Für die Hervorhebung eigener Einträge.</summary>
public class BoolToThicknessConverter : IValueConverter
{
    public static readonly BoolToThicknessConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) ? w : 2.0;
        return value is true ? new Thickness(width) : new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
