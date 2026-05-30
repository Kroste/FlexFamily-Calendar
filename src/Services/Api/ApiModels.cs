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
    bool Masked,
    string? ActivityTypeId = null);

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

public record ServerRecurrenceSkipDto(string Id, DateOnly From, DateOnly To, string? Reason);

public record ServerPlannerNoteDto(string Id, string Text, DateTime CreatedAtUtc);

public record ServerRecurringActivityDto(
    string Id,
    string UserId,
    string UserDisplayName,
    string Title,
    string? ActivityTypeId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    List<int> Weekdays,
    bool SkipOnHolidays,
    List<ServerRecurrenceSkipDto>? Skips);

public record ServerSwapRequestDto(
    string Id,
    string CreatedAt,
    string? RespondedAt,
    int Status,
    int Mode,
    string FromUserId,
    string FromUserName,
    string FromDate,
    string FromEntryId,
    string ToUserId,
    string ToUserName,
    string? ToDate,
    string? ToEntryId,
    string Message);

public record ServerNotificationDto(
    string Id,
    string UserId,
    string CreatedAt,
    bool IsRead,
    string MessageKey,
    List<string> Args,
    string? RelatedDate,
    string? Action,
    string? RelatedUserId);

public record ServerDayNoteDto(string Note, bool IsFinalized);

public record UpdateProfileBody(string? DisplayName, string? Email, string? Language, string? Color);

public record CreateEntryBody(
    string? UserId,
    string Type,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note,
    string? ActivityTypeId = null);

public record UpdateEntryBody(
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool EndsNextDay,
    string? CategoryLabel,
    string? Note,
    string? Type = null,
    string? ActivityTypeId = null);

public record ServerMailRecipientDto(string Email, string PdfBase64);

public record SendWeekPlanBody(
    string Subject,
    string Body,
    string FileName,
    List<ServerMailRecipientDto> Recipients);

public record SendWeekPlanResponseDto(int Sent, int Failed, List<string> Errors);

public record AiCompleteBody(string Provider, string Prompt, string? Model);

public record AiCompleteResponseDto(string Text);
