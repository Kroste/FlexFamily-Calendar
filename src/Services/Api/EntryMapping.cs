using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>
/// Reine Übersetzung zwischen Desktop-<see cref="CalendarEntry"/> und Server-DTOs.
/// Kernunterschied: Der Server speichert Abwesenheiten als EINEN Bereich-Eintrag
/// (Date+EndDate), der Desktop pro Tag einen Eintrag mit gemeinsamer AbsenceGroupId.
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
        _ => EntryType.Work
    };

    public static string TypeToServer(EntryType type) => type.ToString();

    public static bool IsAbsenceType(EntryType t) =>
        t is EntryType.Vacation or EntryType.SickLeave or EntryType.Absence;

    /// <summary>Server-Eintrag → Desktop-Eintrag für den angegebenen Tag.</summary>
    public static CalendarEntry ToDesktop(ServerEntryDto dto, DateOnly day)
    {
        var type = ParseType(dto.Type);
        var e = new CalendarEntry
        {
            Id = dto.Id,
            UserId = dto.UserId,
            Type = type,
            Title = dto.CategoryLabel ?? "",
            ActivityTypeId = NullIfEmpty(dto.ActivityTypeId),   // Kategorie-Referenz → Desktop löst Name/Farbe auf
            Notes = dto.Note ?? ""
        };

        if (IsAbsenceType(type))
        {
            // Ein Server-Bereich = eine Abwesenheits-Gruppe; pro geladenem Tag ein Desktop-Eintrag.
            e.AbsenceGroupId = dto.Id;
            e.AbsenceStart = dto.Date;
            e.AbsenceEnd = dto.EndDate ?? dto.Date;
        }
        else
        {
            e.StartTime = (dto.StartTime ?? new TimeOnly(0, 0)).ToTimeSpan();
            e.EndTime = (dto.EndTime ?? new TimeOnly(0, 0)).ToTimeSpan();
        }
        return e;
    }

    /// <summary>Desktop-Eintrag (an einem konkreten Tag) → Server-Create-Body.</summary>
    public static CreateEntryBody ToCreateBody(CalendarEntry e, DateOnly day)
    {
        if (IsAbsenceType(e.Type))
        {
            var start = e.AbsenceStart ?? day;
            var end = e.AbsenceEnd ?? start;
            return new CreateEntryBody(
                UserId: string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId,
                Type: TypeToServer(e.Type),
                Date: start, EndDate: end,
                StartTime: null, EndTime: null, EndsNextDay: false,
                CategoryLabel: NullIfEmpty(e.Title), Note: NullIfEmpty(e.Notes),
                ActivityTypeId: NullIfEmpty(e.ActivityTypeId));
        }

        return new CreateEntryBody(
            UserId: string.IsNullOrWhiteSpace(e.UserId) ? null : e.UserId,
            Type: TypeToServer(e.Type),
            Date: day, EndDate: null,
            StartTime: TimeOnly.FromTimeSpan(e.StartTime),
            EndTime: TimeOnly.FromTimeSpan(e.EndTime),
            EndsNextDay: e.EndTime <= e.StartTime,   // über Mitternacht
            CategoryLabel: NullIfEmpty(e.Title), Note: NullIfEmpty(e.Notes),
            ActivityTypeId: NullIfEmpty(e.ActivityTypeId));
    }

    /// <summary>Desktop-Eintrag (an einem konkreten Tag) → Server-Update-Body.</summary>
    public static UpdateEntryBody ToUpdateBody(CalendarEntry e, DateOnly day)
    {
        var c = ToCreateBody(e, day);
        return new UpdateEntryBody(c.Date, c.EndDate, c.StartTime, c.EndTime, c.EndsNextDay, c.CategoryLabel, c.Note, c.ActivityTypeId);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
