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
    string Category,
    double WeeklyHoursQuota = 0,
    double MaxWeeklyHours = 0,
    double MaxDailyHours = 0,
    double MinRestHours = 0,
    string? Color = null,
    string? Language = null);

public record LoginResponse(string Token, ServerUserDto User);

public record CreateUserBody(
    string Username,
    string Password,
    string DisplayName,
    string Email,
    string Role,
    string Category,
    double WeeklyHoursQuota,
    double MaxWeeklyHours,
    double MaxDailyHours,
    double MinRestHours,
    string Color,
    string Language);

public record UpdateUserBody(
    string Username,
    string DisplayName,
    string Email,
    string Role,
    string Category,
    double WeeklyHoursQuota,
    double MaxWeeklyHours,
    double MaxDailyHours,
    double MinRestHours,
    string Color,
    string Language);

public record ApiErrorBody(string? Error);

public record ServerActivityTypeDto(string Id, string Name, string Color, List<string> Categories);

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
