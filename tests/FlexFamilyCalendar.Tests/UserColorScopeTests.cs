using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class UserColorScopeTests
{
    private static async Task<(AuthService auth, User user)> SeedAsync(string color)
    {
        var auth = new AuthService(new InMemoryStorageService());
        await auth.CreateUserAsync(new User
        {
            Username = "anna", DisplayName = "Anna", Category = PersonCategory.Employee, Color = color
        }, "pw");
        var user = (await auth.GetUsersAsync()).Single();
        return (auth, user);
    }

    [Fact]
    public async Task SelfMode_DoesNotChangeColor_ButSavesOtherFields()
    {
        var (auth, user) = await SeedAsync("#2E86C1");
        var vm = new UserEditorViewModel(auth, user, isNew: false, selfMode: true);

        vm.SelectedColor = "#E84393";          // Pink – darf NICHT greifen
        vm.DisplayName = "Anna B.";            // soll greifen
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = (await auth.GetUsersAsync()).Single();
        Assert.Equal("#2E86C1", saved.Color);  // Farbe unverändert
        Assert.Equal("Anna B.", saved.DisplayName);
    }

    [Fact]
    public async Task AdminMode_ChangesColor()
    {
        var (auth, user) = await SeedAsync("#2E86C1");
        var vm = new UserEditorViewModel(auth, user, isNew: false, selfMode: false);

        vm.SelectedColor = "#E84393";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("#E84393", (await auth.GetUsersAsync()).Single().Color);
    }
}
