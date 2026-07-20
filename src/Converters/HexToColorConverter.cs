using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FlexFamilyCalendar.Converters;

/// <summary>
/// Zwei-Wege-Konverter zwischen Hex-Farb-String (z.B. "#2E86C1") und Avalonia
/// <see cref="Color"/>. Wird für das Binden des <c>Avalonia.Controls.ColorPicker</c>
/// gegen ein string-basiertes VM-Feld (User.Color) genutzt.
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public static readonly HexToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s) && Color.TryParse(s, out var c))
            return c;
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        return null;
    }
}
