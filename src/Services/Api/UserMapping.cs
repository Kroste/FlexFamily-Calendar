using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt einen Server-Benutzer in das Desktop-<see cref="User"/>-Modell.</summary>
public static class UserMapping
{
    public static User ToDesktop(ServerUserDto d) => new()
    {
        Id = d.Id,
        Username = d.Username,
        DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.Username : d.DisplayName,
        Email = d.Email ?? "",
        Role = string.Equals(d.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? UserRole.Admin : UserRole.User,
        Category = Enum.TryParse<PersonCategory>(d.Category, out var c) ? c : PersonCategory.Employee
    };
}
