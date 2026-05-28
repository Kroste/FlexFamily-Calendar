using FlexFamilyCalendar.Models;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class ActivityTypeTests
{
    [Fact]
    public void AppliesTo_OnlyAssignedRoles()
    {
        var t = new ActivityType
        {
            Name = "Sport",
            Categories = { PersonCategory.Child, PersonCategory.AuPair }
        };

        Assert.True(t.AppliesTo(PersonCategory.Child));
        Assert.True(t.AppliesTo(PersonCategory.AuPair));
        Assert.False(t.AppliesTo(PersonCategory.Parent));
        Assert.False(t.AppliesTo(PersonCategory.Employee));
    }

    [Fact]
    public async Task Storage_RoundTrip_PreservesCategories()
    {
        var storage = new InMemoryStorageService();
        var types = new List<ActivityType>
        {
            new() { Name = "Schule", Color = "#3498DB", Categories = { PersonCategory.Child } },
            new() { Name = "Sprachkurs", Color = "#9B59B6", Categories = { PersonCategory.AuPair, PersonCategory.Employee } }
        };

        await storage.SaveActivityTypesAsync(types);
        var loaded = await storage.LoadActivityTypesAsync();

        Assert.Equal(2, loaded.Count);
        var sprach = loaded.Single(t => t.Name == "Sprachkurs");
        Assert.Equal("#9B59B6", sprach.Color);
        Assert.Equal(new[] { PersonCategory.AuPair, PersonCategory.Employee }, sprach.Categories);
    }

    [Fact]
    public async Task Storage_Empty_WhenNothingSaved()
        => Assert.Empty(await new InMemoryStorageService().LoadActivityTypesAsync());
}
