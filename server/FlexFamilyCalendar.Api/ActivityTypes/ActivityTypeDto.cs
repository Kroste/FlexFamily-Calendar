using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.ActivityTypes;

/// <summary>Aktivitäts-Kategorie für Lesen und Ersetzen (PUT). <see cref="Categories"/> = PersonCategory-Namen.</summary>
public record ActivityTypeDto(Guid Id, string Name, string Color, List<string> Categories)
{
    public static ActivityTypeDto From(ActivityTypeEntity e) => new(e.Id, e.Name, e.Color, e.Categories);
}
