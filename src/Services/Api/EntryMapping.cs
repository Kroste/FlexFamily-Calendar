using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>
/// Reine Übersetzung zwischen Desktop-<see cref="CalendarEntry"/> und Server-DTOs.
/// Kernunterschied: Der Server speichert Zeiträume (Abwesenheiten und mehrtägige Einträge) als
/// EINEN Bereich-Eintrag (Date+EndDate, Uhrzeit am ersten und letzten Tag), der Desktop pro Tag
/// einen Eintrag mit gemeinsamer AbsenceGroupId, der nur den Anteil dieses Tages trägt.
/// Ohne Uhrzeit (null) ist ein Eintrag ganztägig.
/// </summary>
public static class EntryMapping
{
    public static EntryType ParseType(string serverType) => serverType switch
    {
        "Work" => EntryType.Work,
        "Vacation" => EntryType.Vacation,
        "SickLeave" => EntryType.SickLeave,
        "Activity" => EntryType.Activity,
        "Absence" => EntryType.Absence,
        "Overnight" => EntryType.Overnight,
        "Custom" => EntryType.Custom,
        _ => EntryType.Work
    };

    public static string TypeToServer(EntryType type) => type.ToString();

    public static bool IsAbsenceType(EntryType t) =>
        t is EntryType.Vacation or EntryType.SickLeave or EntryType.Absence;

    /// <summary>
    /// Wird der Eintrag als Zeitraum geführt (je Tag ein Desktop-Eintrag, auf dem Server EINER)?
    /// Abwesenheiten immer, alles andere, sobald es mehr als einen Tag spannt.
    /// </summary>
    public static bool IsRange(CalendarEntry e)
        => IsAbsenceType(e.Type) || !string.IsNullOrEmpty(e.AbsenceGroupId);

    private static bool IsRange(ServerEntryDto dto)
        => IsAbsenceType(ParseType(dto.Type)) || (dto.EndDate ?? dto.Date) > dto.Date;

    /// <summary>Ganztägig = ohne Uhrzeit. So speichert der Server es, so kommt es an.</summary>
    private static bool IsAllDay(ServerEntryDto dto) => dto.StartTime is null || dto.EndTime is null;

    /// <summary>
    /// Deckt ein Server-Eintrag diesen Tag ab? Ein Zeitraum ist EIN Eintrag und gehört an jeden
    /// Tag darin — beim Bereichs-Abruf einer Woche muss der Client das selbst aufteilen, während
    /// der tageweise Abruf es vom Server bekam. Endet ein Zeitraum genau um Mitternacht, hat sein
    /// letzter Tag keinen Anteil und bekommt keine Kachel.
    /// </summary>
    public static bool CoversDay(ServerEntryDto dto, DateOnly date)
    {
        var end = dto.EndDate ?? dto.Date;
        if (dto.Date > date || end < date) return false;
        return !EntrySpans.IsEmptyLastDay(date, dto.Date, end, IsAllDay(dto), dto.EndTime?.ToTimeSpan());
    }

    /// <summary>Server-Eintrag → Desktop-Eintrag für den angegebenen Tag.</summary>
    public static CalendarEntry ToDesktop(ServerEntryDto dto, DateOnly day)
    {
        var type = ParseType(dto.Type);
        var allDay = IsAllDay(dto);
        var e = new CalendarEntry
        {
            Id = dto.Id,
            UserId = dto.UserId,
            Type = type,
            AllDay = allDay,
            // Server-Status übernehmen (Pending/Approved/Rejected). Approved-Default deckt den
            // Fall ab, dass eine ältere Server-Version das Feld noch nicht mitliefert.
            Status = string.IsNullOrWhiteSpace(dto.Status) ? EntryStatuses.Approved : dto.Status,
            Title = dto.CategoryLabel ?? "",
            // Bei maskierten Einträgen liefert der Server hier bewusst nichts (EntryDto.Mask) —
            // eine Sonderfarbe würde die Maskierung sonst über die Optik unterlaufen.
            Color = dto.Color ?? "",
            Notes = dto.Note ?? ""
        };

        var startTime = allDay ? (TimeSpan?)null : dto.StartTime!.Value.ToTimeSpan();
        var endTime = allDay ? (TimeSpan?)null : dto.EndTime!.Value.ToTimeSpan();

        if (IsRange(dto))
        {
            // Ein Server-Bereich = ein Zeitraum; pro geladenem Tag ein Desktop-Eintrag mit dem
            // Anteil dieses Tages.
            var end = dto.EndDate ?? dto.Date;
            e.AbsenceGroupId = dto.Id;
            e.AbsenceStart = dto.Date;
            e.AbsenceEnd = end;
            e.SpanStartTime = startTime;
            e.SpanEndTime = endTime;
            (e.StartTime, e.EndTime) = EntrySpans.DayPortion(day, dto.Date, end, allDay, startTime, endTime);
        }
        else
        {
            e.StartTime = startTime ?? TimeSpan.Zero;
            e.EndTime = endTime ?? TimeSpan.Zero;
        }
        return e;
    }

    /// <summary>Desktop-Eintrag (an einem konkreten Tag) → Server-Create-Body.</summary>
    public static CreateEntryBody ToCreateBody(CalendarEntry e, DateOnly day)
    {
        var allDay = e.IsAllDay;
        if (IsRange(e))
        {
            var start = e.AbsenceStart ?? day;
            var end = e.AbsenceEnd ?? start;
            return new CreateEntryBody(
                UserId: string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId,
                Type: TypeToServer(e.Type),
                Date: start, EndDate: end,
                // Die Eckzeiten des ganzen Zeitraums, nicht der Anteil des Tages, an dem gespeichert wird.
                StartTime: allDay ? null : TimeOnly.FromTimeSpan(e.SpanStartTime ?? e.StartTime),
                EndTime: allDay ? null : TimeOnly.FromTimeSpan(e.SpanEndTime ?? ClampToDay(e.EndTime)),
                EndsNextDay: false,
                CategoryLabel: NullIfEmpty(e.Title), Note: NullIfEmpty(e.Notes),
                Color: NullIfEmpty(e.Color));
        }

        return new CreateEntryBody(
            UserId: string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId,
            Type: TypeToServer(e.Type),
            Date: day, EndDate: null,
            StartTime: allDay ? null : TimeOnly.FromTimeSpan(e.StartTime),
            EndTime: allDay ? null : TimeOnly.FromTimeSpan(ClampToDay(e.EndTime)),
            EndsNextDay: e.CrossesMidnight,   // über Mitternacht
            CategoryLabel: NullIfEmpty(e.Title), Note: NullIfEmpty(e.Notes),
            Color: NullIfEmpty(e.Color));
    }

    /// <summary>24:00 (Tagesende eines Zeitraum-Anteils) kennt <see cref="TimeOnly"/> nicht — dort ist es 00:00.</summary>
    private static TimeSpan ClampToDay(TimeSpan t) => t >= EntrySpans.EndOfDay ? TimeSpan.Zero : t;

    /// <summary>Desktop-Eintrag (an einem konkreten Tag) → Server-Update-Body. Type wird
    /// mitgeschickt, damit ein Wechsel im Bearbeiten-Dialog (z.B. Arbeit → Aktivität) tatsächlich
    /// auf dem Server landet — sonst rauscht die UPDATE-Sicht am Type vorbei.</summary>
    public static UpdateEntryBody ToUpdateBody(CalendarEntry e, DateOnly day)
    {
        var c = ToCreateBody(e, day);
        return new UpdateEntryBody(c.Date, c.EndDate, c.StartTime, c.EndTime, c.EndsNextDay,
            c.CategoryLabel, c.Note, c.Type, c.Color);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
