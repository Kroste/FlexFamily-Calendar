using Avalonia.Data.Converters;
using System.Globalization;

namespace FlexFamilyCalendar.Localization;

/// <summary>Wandelt einen Localizer-Schlüssel (string) in den übersetzten Text.</summary>
public class LocKeyConverter : IValueConverter
{
    public static readonly LocKeyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key ? Localizer.Instance[key] : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
