using Avalonia.Data.Converters;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using System.Globalization;

namespace FlexFamilyCalendar.Converters;

/// <summary>EntryType → lokalisierte Beschriftung über den Localizer.</summary>
public class EntryTypeLabelConverter : IValueConverter
{
    public static readonly EntryTypeLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EntryType type ? Localizer.Instance[EntryTypeInfo.Key(type)] : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
