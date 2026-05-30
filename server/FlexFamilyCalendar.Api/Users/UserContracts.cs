using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Users;

/// <summary>Benutzer-Antwort an den Client (ohne PasswordHash).</summary>
public record UserDto(
    Guid Id,
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
    string Language,
    string AiStyleHint)
{
    public static UserDto From(UserEntity u) => new(
        u.Id, u.Username, u.DisplayName, u.Email, u.Role, u.Category,
        u.WeeklyHoursQuota, u.MaxWeeklyHours, u.MaxDailyHours, u.MinRestHours,
        u.Color, u.Language, u.AiStyleHint);
}

/// <summary>Reine Invarianten der Benutzerverwaltung (serverseitig erzwungen, testbar).</summary>
public static class UserRules
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static string NormalizeRole(string? role) => role == Admin ? Admin : User;

    /// <summary>Der letzte Admin darf nicht herabgestuft werden.</summary>
    public static string? CheckRoleChange(bool targetIsCurrentlyAdmin, bool newRoleIsAdmin, int totalAdmins)
        => targetIsCurrentlyAdmin && !newRoleIsAdmin && totalAdmins <= 1
            ? "Es muss mindestens ein Administrator bestehen bleiben."
            : null;

    /// <summary>Der letzte Admin darf nicht gelöscht werden.</summary>
    public static string? CheckDelete(bool targetIsAdmin, int totalAdmins)
        => targetIsAdmin && totalAdmins <= 1
            ? "Der letzte Administrator kann nicht gelöscht werden."
            : null;
}
