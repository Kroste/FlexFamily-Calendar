using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class ActivityTypeMappingTests
{
    [Fact]
    public void ToDesktop_maps_fields_and_category_names()
    {
        var dto = new ServerActivityTypeDto("a1", "Sport", "#3498DB",
            new List<string> { "Child", "AuPair" });

        var t = ActivityTypeMapping.ToDesktop(dto);

        Assert.Equal("a1", t.Id);
        Assert.Equal("Sport", t.Name);
        Assert.Equal("#3498DB", t.Color);
        Assert.Equal(new[] { PersonCategory.Child, PersonCategory.AuPair }, t.Categories);
    }

    [Fact]
    public void ToDesktop_ignores_unknown_category_names()
    {
        var dto = new ServerActivityTypeDto("a1", "X", "", new List<string> { "Child", "Nonsense" });
        var t = ActivityTypeMapping.ToDesktop(dto);
        Assert.Equal(new[] { PersonCategory.Child }, t.Categories);
    }

    [Fact]
    public void ToServer_serializes_categories_as_names()
    {
        var t = new ActivityType
        {
            Id = "a2",
            Name = "Schule",
            Color = "#E67E22",
            Categories = new List<PersonCategory> { PersonCategory.Child }
        };

        var dto = ActivityTypeMapping.ToServer(t);

        Assert.Equal("a2", dto.Id);
        Assert.Equal("Schule", dto.Name);
        Assert.Equal(new[] { "Child" }, dto.Categories);
    }

    [Fact]
    public void Round_trip_preserves_categories()
    {
        var t = new ActivityType
        {
            Id = "a3", Name = "Musik", Color = "#9B59B6",
            Categories = new List<PersonCategory> { PersonCategory.Parent, PersonCategory.Employee }
        };

        var back = ActivityTypeMapping.ToDesktop(ActivityTypeMapping.ToServer(t));

        Assert.Equal(t.Categories, back.Categories);
        Assert.Equal(t.Name, back.Name);
    }
}
