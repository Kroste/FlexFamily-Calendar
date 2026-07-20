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
    public async Task SelfMode_ChangesColor_AlongsideOtherFields()
    {
        // Personenfarbe ist ab v0.1.34 eine Anzeige-Präferenz und darf jeder selbst wählen —
        // Self-Mode setzt die Farbe genauso wie andere pflegbare Profil-Felder.
        var (auth, user) = await SeedAsync("#2E86C1");
        var vm = new UserEditorViewModel(auth, user, isNew: false, selfMode: true);

        vm.SelectedColor = "#E84393";
        vm.DisplayName = "Anna B.";
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = (await auth.GetUsersAsync()).Single();
        Assert.Equal("#E84393", saved.Color);
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
