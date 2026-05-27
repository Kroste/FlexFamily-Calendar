namespace FlexFamilyCalendar.Controls;

/// <summary>Geteilte Maße für Zeitachse, Stundenraster und Eintrags-Positionierung.</summary>
public static class CalendarMetrics
{
    public const double PixelsPerHour = 48.0;
    public const int HourCount = 24;
    public const double DayHeight = HourCount * PixelsPerHour;

    /// <summary>"00:00" … "23:00" — für Achsen-Labels und Rasterzellen (per {x:Static} im XAML).</summary>
    public static IReadOnlyList<string> HourLabels { get; } =
        Enumerable.Range(0, HourCount).Select(h => $"{h:D2}:00").ToList();
}
