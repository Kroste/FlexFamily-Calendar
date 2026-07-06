using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Tests;

// Der About-/Info-Dialog liefert Namen, Version und die statischen Links. Wir sichern die
// Verträge (Format, Nicht-Leer, korrekter GitHub-Owner/Repo, CoffeeUrl gegen buymeacoffee),
// damit ein Redesign nicht versehentlich den BMC-Button oder den GitHub-Link verliert
// (Master-CLAUDE.md-Anforderung).
public class InfoViewModelTests
{
    [Fact]
    public void AppName_ist_stabil()
    {
        var vm = new InfoViewModel();
        Assert.Equal("FlexFamily Calendar", vm.AppName);
    }

    [Fact]
    public void AppVersion_ist_nicht_leer()
    {
        var vm = new InfoViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
    }

    [Fact]
    public void GitHubUrl_zeigt_auf_Kroste_FlexFamily_Calendar()
    {
        var vm = new InfoViewModel();
        Assert.Equal("https://github.com/Kroste/FlexFamily-Calendar", vm.GitHubUrl);
    }

    [Fact]
    public void CoffeeUrl_zeigt_auf_buymeacoffee()
    {
        var vm = new InfoViewModel();
        Assert.StartsWith("https://buymeacoffee.com/", vm.CoffeeUrl);
    }

    [Fact]
    public void CloseCommand_feuert_CloseRequested()
    {
        var vm = new InfoViewModel();
        var fired = 0;
        vm.CloseRequested += () => fired++;

        vm.CloseCommand.Execute(null);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Description_ist_nicht_leer()
    {
        var vm = new InfoViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.Description));
    }
}
