using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt zwischen Server-DTO und Desktop-<see cref="ActivityType"/> (Kategorien als Enum-Namen).</summary>
public static class ActivityTypeMapping
{
    public static ActivityType ToDesktop(ServerActivityTypeDto d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Color = d.Color ?? "",
        Categories = (d.Categories ?? new())
            .Select(s => Enum.TryParse<PersonCategory>(s, out var c) ? (PersonCategory?)c : null)
            .Where(c => c is not null)
            .Select(c => c!.Value)
            .ToList()
    };

    public static ServerActivityTypeDto ToServer(ActivityType a) => new(
        a.Id, a.Name, a.Color, a.Categories.Select(c => c.ToString()).ToList());
}
