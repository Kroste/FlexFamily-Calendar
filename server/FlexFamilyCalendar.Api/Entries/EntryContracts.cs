using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Entries;

/// <summary>Was ein Client zu sehen bekommt. Bei <see cref="Masked"/>=true sind private Details entfernt.</summary>
public record EntryDto(
    Guid Id,
    Guid UserId,
    string Type,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note,
    string Status,
    bool Masked)
{
    public static EntryDto Full(CalendarEntry e) => new(
        e.Id, e.UserId, e.Type, e.Date, e.EndDate,
        e.StartTime, e.EndTime, e.EndsNextDay,
        e.CategoryLabel, e.Note, e.Status, Masked: false);

    /// <summary>Privat-Eintrag für Fremde: nur „Abwesend" + Zeitraum, ohne Typ/Notiz/Uhrzeit.</summary>
    public static EntryDto Mask(CalendarEntry e) => new(
        e.Id, e.UserId, EntryTypes.Absence, e.Date, e.EndDate,
        StartTime: null, EndTime: null, EndsNextDay: false,
        CategoryLabel: null, Note: null, e.Status, Masked: true);
}

public record CreateEntryRequest(
    Guid? UserId,
    string Type,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note);

public record UpdateEntryRequest(
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note);
