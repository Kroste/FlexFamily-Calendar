using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FlexFamilyCalendar.Converters;

public class ColorStringToBrushConverter : IValueConverter
{
    public static readonly ColorStringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
            return new SolidColorBrush(Color.Parse(colorStr));
        return new SolidColorBrush(Color.Parse("#2E86C1"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
