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
    string? ActivityTypeId,
    string? Note,
    string Status,
    bool Masked,
    string? Color = null)
{
    public static EntryDto Full(CalendarEntry e) => new(
        e.Id, e.UserId, e.Type, e.Date, e.EndDate,
        e.StartTime, e.EndTime, e.EndsNextDay,
        e.CategoryLabel, e.ActivityTypeId, e.Note, e.Status, Masked: false, e.Color);

    /// <summary>Privat-Eintrag für Fremde: nur „Abwesend" + Zeitraum, ohne Typ/Notiz/Uhrzeit/Kategorie.
    /// Die frei gewählte Farbe fällt hier ebenfalls weg: eine Sonderfarbe würde die Maskierung
    /// unterlaufen, weil sich der Eintrag damit von anderen Abwesenheiten unterscheiden ließe.</summary>
    public static EntryDto Mask(CalendarEntry e) => new(
        e.Id, e.UserId, EntryTypes.Absence, e.Date, e.EndDate,
        StartTime: null, EndTime: null, EndsNextDay: false,
        CategoryLabel: null, ActivityTypeId: null, Note: null, e.Status, Masked: true, Color: null);
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
    string? Note,
    string? ActivityTypeId = null,
    string? Color = null);

public record UpdateEntryRequest(
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note,
    string? Type = null,                 // null = Typ unverändert, sonst neuer Typ
    string? ActivityTypeId = null,
    string? Color = null);
