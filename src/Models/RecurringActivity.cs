using System.Globalization;

namespace FlexFamilyCalendar.Models;

/// <summary>
/// Eine wiederkehrende Aktivität (Regel), z.B. „Do 16:00 Fußball" oder „täglich 19:00 Sprachkurs".
/// Wird nicht pro Tag persistiert, sondern als Overlay über die Tages-Einträge projiziert
/// (siehe <see cref="Services.RecurrenceEngine"/>). Vom Typ <see cref="EntryType.Activity"/>.
/// </summary>
public class RecurringActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = "";
    public string UserDisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string? ActivityTypeId { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Wochentage, an denen die Aktivität stattfindet (alle sieben = täglich).</summary>
    public List<DayOfWeek> Weekdays { get; set; } = new();

    /// <summary>true → an Feiertagen ausgeblendet („außer Feiertag"); false → angezeigt mit Hinweis „könnte ausfallen".</summary>
    public bool SkipOnHolidays { get; set; }

    /// <summary>Findet die Regel an diesem Datum statt? (Reine Wochentags-Prüfung.)</summary>
    public bool OccursOn(DateOnly date) => Weekdays.Contains(date.DayOfWeek);

    /// <summary>Aufgelöster Kategoriename (Laufzeit; für die Verwaltungsliste). Die Kategorie ist der Name im Kalender.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string CategoryName { get; set; } = "";

    [Newtonsoft.Json.JsonIgnore]
    public string TimeRange => $"{StartTime:hh\\:mm}–{EndTime:hh\\:mm}";

    /// <summary>Kompakte Wochentags-Liste (Mo→So-Reihenfolge, kulturabhängige Kurznamen).</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string WeekdaysLabel => string.Join(", ", Weekdays
        .OrderBy(WeekOrder)
        .Select(d => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(d)));

    private static int WeekOrder(DayOfWeek d) => ((int)d + 6) % 7;  // Mo=0 … So=6
}
