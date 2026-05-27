using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class UserColorTests
{
    [Fact]
    public void Palette_HasDistinctColors()
    {
        var colors = UserColorPalette.Colors;
        Assert.True(colors.Count >= 6);
        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public void ColorAt_CyclesAndStaysInPalette()
    {
        var n = UserColorPalette.Colors.Count;
        Assert.Equal(UserColorPalette.Colors[0], UserColorPalette.ColorAt(0));
        Assert.Equal(UserColorPalette.Colors[0], UserColorPalette.ColorAt(n));   // zykliert
        Assert.Contains(UserColorPalette.ColorAt(999), UserColorPalette.Colors);
    }

    [Fact]
    public async Task NewUsers_GetDifferentAutoColors()
    {
        var auth = new AuthService(new InMemoryStorageService());
        await auth.CreateUserAsync(new User { Username = "a", Category = PersonCategory.Employee }, "pw");
        await auth.CreateUserAsync(new User { Username = "b", Category = PersonCategory.Employee }, "pw");

        var users = await auth.GetUsersAsync();
        var a = users.First(u => u.Username == "a");
        var b = users.First(u => u.Username == "b");

        Assert.False(string.IsNullOrEmpty(a.Color));
        Assert.False(string.IsNullOrEmpty(b.Color));
        Assert.NotEqual(a.Color, b.Color);
    }

    [Fact]
    public async Task ExplicitColor_IsKept()
    {
        var auth = new AuthService(new InMemoryStorageService());
        await auth.CreateUserAsync(new User { Username = "a", Category = PersonCategory.Employee, Color = "#123456" }, "pw");

        Assert.Equal("#123456", (await auth.GetUsersAsync()).Single().Color);
    }
}
