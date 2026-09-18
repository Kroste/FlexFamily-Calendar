using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Was der Dialog aus Start und Ende gemacht hat — die Form, in der gespeichert wird.</summary>
/// <param name="From">Erster Tag.</param>
/// <param name="To">Letzter Tag (bei einer Nachtschicht derselbe wie <paramref name="From"/>).</param>
/// <param name="AllDay">Ganztägig, ohne Uhrzeit.</param>
/// <param name="StartTime">Uhrzeit am ersten Tag (null bei ganztägig).</param>
/// <param name="EndTime">Uhrzeit am letzten Tag (null bei ganztägig).</param>
/// <param name="IsSpan">Als Zeitraum speichern (je Tag ein Eintrag mit gemeinsamer GroupId)
/// statt als einzelner Tageseintrag.</param>
public record SpanPlan(DateOnly From, DateOnly To, bool AllDay, TimeSpan? StartTime, TimeSpan? EndTime, bool IsSpan);

/// <summary>
/// Start und Ende mit Datum und Uhrzeit, wie im Google-Kalender — für Einträge und Abwesenheiten.
/// Reine Logik, UI-unabhängig und testbar.
///
/// Gespeichert wird weiter tageweise: ein Zeitraum wird in je einen Eintrag pro Tag zerlegt, der
/// nur den Anteil dieses Tages trägt (erster Tag „ab 14:00", Mitte ganztägig, letzter Tag
/// „bis 10:00"). So bleibt jeder Tag für sich lesbar — Plan, Stunden, PDF und Mail arbeiten
/// weiter auf Tagen. Die Eckwerte des ganzen Zeitraums stehen zusätzlich an jedem Tag
/// (<see cref="CalendarEntry.SpanStartTime"/>/<see cref="CalendarEntry.SpanEndTime"/>), damit sich
/// der Zeitraum von jedem seiner Tage aus bearbeiten lässt.
///
/// Eine Nachtschicht (Mo 20:00 → Di 06:00) bleibt bewusst ein Eintrag am Starttag, der über
/// Mitternacht läuft — so wie bisher. Als Zeitraum zerlegt, hätte sie zwei Kacheln und ihre
/// Stunden verteilten sich auf zwei Tage; Tausch und Ruhezeit-Prüfung kennen sie aber als eine
/// Schicht.
/// </summary>
public static class EntrySpans
{
    /// <summary>Tagesende als Uhrzeit (24:00) — Ende des Anteils, der bis Mitternacht läuft.</summary>
    public static readonly TimeSpan EndOfDay = TimeSpan.FromHours(24);

    /// <summary>
    /// Deutet die Eingaben im Dialog. Liefert entweder die Speicherform oder den
    /// Lokalisierungs-Schlüssel des Fehlers.
    /// </summary>
    public static (SpanPlan? Plan, string? ErrorKey) Resolve(
        DateOnly startDate, TimeSpan? startTime, DateOnly endDate, TimeSpan? endTime, bool allDay, bool isAbsence)
    {
        if (endDate < startDate) return (null, "Entry_ErrorEndBeforeStart");

        // Abwesenheiten laufen immer als Zeitraum (auch eintägig) — daran hängen Genehmigung
        // und Aufräumen über die GroupId.
        if (allDay)
            return (new SpanPlan(startDate, endDate, true, null, null, isAbsence || endDate > startDate), null);

        if (startTime is not { } st) return (null, "Entry_ErrorNoStart");
        if (endTime is not { } et) return (null, "Entry_ErrorNoEnd");

        if (endDate == startDate)
        {
            if (et == st) return (null, "Entry_ErrorSameTime");
            // Ende vor Start am selben Tag: Schicht über Mitternacht, wie vor dem Umbau.
            return (new SpanPlan(startDate, startDate, false, st, et, isAbsence), null);
        }

        // Nachtschicht: endet am Folgetag, bevor die Startzeit wieder erreicht ist.
        if (!isAbsence && endDate == startDate.AddDays(1) && et <= st)
            return (new SpanPlan(startDate, startDate, false, st, et, false), null);

        return (new SpanPlan(startDate, endDate, false, st, et, true), null);
    }

    /// <summary>
    /// Anteil eines Tages an einem Zeitraum: erster Tag ab der Startzeit bis 24:00, mittlere Tage
    /// ganz, letzter Tag ab 00:00 bis zur Endzeit. Ganztägig: keine Uhrzeit (00:00/00:00).
    /// </summary>
    public static (TimeSpan Start, TimeSpan End) DayPortion(
        DateOnly day, DateOnly from, DateOnly to, bool allDay, TimeSpan? startTime, TimeSpan? endTime)
    {
        if (allDay) return (TimeSpan.Zero, TimeSpan.Zero);
        var start = startTime ?? TimeSpan.Zero;
        var end = endTime ?? TimeSpan.Zero;
        if (from == to) return (start, end);
        return (day == from ? start : TimeSpan.Zero, day == to ? end : EndOfDay);
    }

    /// <summary>
    /// Ein Zeitraum, der genau um Mitternacht endet (Mo 14:00 → Mi 00:00), hat am letzten Tag
    /// keinen Anteil — der fällt weg, statt als leere Kachel „bis 00:00" zu stehen.
    /// </summary>
    public static bool IsEmptyLastDay(DateOnly day, DateOnly from, DateOnly to, bool allDay, TimeSpan? endTime)
        => !allDay && to > from && day == to && (endTime ?? TimeSpan.Zero) == TimeSpan.Zero;

    /// <summary>
    /// Zerlegt einen Zeitraum in je einen Eintrag pro Tag, verbunden über <paramref name="groupId"/>.
    /// Ganztägig und Eckzeiten nimmt die Vorlage aus <see cref="CalendarEntry.AllDay"/> und
    /// <see cref="CalendarEntry.SpanStartTime"/>/<see cref="CalendarEntry.SpanEndTime"/>.
    /// </summary>
    public static List<(DateOnly Date, CalendarEntry Entry)> Build(
        CalendarEntry template, DateOnly from, DateOnly to, string groupId)
    {
        if (to < from) (from, to) = (to, from);
        var allDay = template.IsAllDay;
        var startTime = allDay ? null : template.SpanStartTime;
        var endTime = allDay ? null : template.SpanEndTime;

        var result = new List<(DateOnly, CalendarEntry)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (IsEmptyLastDay(d, from, to, allDay, endTime)) continue;
            var (s, e) = DayPortion(d, from, to, allDay, startTime, endTime);
            result.Add((d, new CalendarEntry
            {
                Id = Guid.NewGuid().ToString(),
                UserId = template.UserId,
                UserDisplayName = template.UserDisplayName,
                Type = template.Type,
                Status = template.Status,
                AllDay = allDay,
                StartTime = s,
                EndTime = e,
                SpanStartTime = startTime,
                SpanEndTime = endTime,
                Title = template.Title,
                Notes = template.Notes,
                Color = template.Color,      // gewählte Kachelfarbe gilt für den ganzen Zeitraum
                AbsenceGroupId = groupId,
                AbsenceStart = from,
                AbsenceEnd = to
            }));
        }
        return result;
    }
}
