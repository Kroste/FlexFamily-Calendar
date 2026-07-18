using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Converters;

/// <summary>
/// Filtert eine Entry-Liste auf die Einträge einer bestimmten User-Id — für den Mobile-
/// Kalender, der bewusst nur die eigenen Schichten zeigt. Web/Desktop nutzen die
/// ungefilterte Liste weiter.
/// </summary>
public class OwnEntriesFilterConverter : IMultiValueConverter
{
    public static readonly OwnEntriesFilterConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not IEnumerable src)
            return values[0];
        var userId = values[1]?.ToString() ?? "";
        if (string.IsNullOrEmpty(userId)) return src;

        var result = new List<CalendarEntry>();
        foreach (var item in src)
        {
            if (item is CalendarEntry e && e.UserId == userId)
                result.Add(e);
        }
        return result;
    }
}
