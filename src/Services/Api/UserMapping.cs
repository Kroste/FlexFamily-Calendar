using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt zwischen Server-Benutzer-DTO und Desktop-<see cref="User"/>-Modell.</summary>
public static class UserMapping
{
    public static User ToDesktop(ServerUserDto d) => new()
    {
        Id = d.Id,
        Username = d.Username,
        DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.Username : d.DisplayName,
        Email = d.Email ?? "",
        Role = string.Equals(d.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? UserRole.Admin : UserRole.User,
        Category = Enum.TryParse<PersonCategory>(d.Category, out var c) ? c : PersonCategory.Employee,
        WeeklyHoursQuota = d.WeeklyHoursQuota,
        MaxWeeklyHours = d.MaxWeeklyHours,
        MaxDailyHours = d.MaxDailyHours,
        MinRestHours = d.MinRestHours,
        Color = d.Color ?? "",
        Language = string.IsNullOrWhiteSpace(d.Language) ? "de" : d.Language!,
        AiStyleHint = d.AiStyleHint ?? "",
        OpeningBalanceHours = d.OpeningBalanceHours,
        AccountStart = d.AccountStart,
        ThemeVariant = string.IsNullOrWhiteSpace(d.ThemeVariant) ? "System" : d.ThemeVariant!,
        ShowHolidays = d.ShowHolidays,
        ShowHints = d.ShowHints,
        OnboardingSeen = d.OnboardingSeen,
        PlanOrder = d.PlanOrder
    };

    public static CreateUserBody ToCreateBody(User u, string password) => new(
        u.Username, password, u.DisplayName, u.Email,
        RoleToServer(u.Role), u.Category.ToString(),
        u.WeeklyHoursQuota, u.MaxWeeklyHours, u.MaxDailyHours, u.MinRestHours,
        u.Color, string.IsNullOrWhiteSpace(u.Language) ? "de" : u.Language,
        string.IsNullOrWhiteSpace(u.AiStyleHint) ? null : u.AiStyleHint,
        u.OpeningBalanceHours, u.AccountStart,
        string.IsNullOrWhiteSpace(u.ThemeVariant) ? "System" : u.ThemeVariant, u.ShowHolidays, u.ShowHints, u.PlanOrder);

    public static UpdateUserBody ToUpdateBody(User u) => new(
        u.Username, u.DisplayName, u.Email,
        RoleToServer(u.Role), u.Category.ToString(),
        u.WeeklyHoursQuota, u.MaxWeeklyHours, u.MaxDailyHours, u.MinRestHours,
        u.Color, string.IsNullOrWhiteSpace(u.Language) ? "de" : u.Language,
        string.IsNullOrWhiteSpace(u.AiStyleHint) ? null : u.AiStyleHint,
        u.OpeningBalanceHours, u.AccountStart,
        string.IsNullOrWhiteSpace(u.ThemeVariant) ? "System" : u.ThemeVariant, u.ShowHolidays, u.ShowHints, u.PlanOrder);

    public static string RoleToServer(UserRole role) => role == UserRole.Admin ? "Admin" : "User";
}
