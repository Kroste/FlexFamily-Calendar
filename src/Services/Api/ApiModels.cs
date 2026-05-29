namespace FlexFamilyCalendar.Services.Api;

// DTOs für die Server-API (FlexFamilyCalendar.Api). Feldnamen passen zu den JSON-Antworten
// des Servers (System.Text.Json, camelCase, Groß-/Kleinschreibung egal beim Deserialisieren).

public record ServerEntryDto(
    string Id,
    string UserId,
    string Type,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note,
    string Status,
    bool Masked);

public record ServerUserDto(
    string Id,
    string Username,
    string DisplayName,
    string? Email,
    string Role,
    string Category);

public record LoginResponse(string Token, ServerUserDto User);

public record CreateEntryBody(
    string? UserId,
    string Type,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note);

public record UpdateEntryBody(
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note);
