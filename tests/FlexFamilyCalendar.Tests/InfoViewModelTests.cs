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

    [Fact]
    public void Ohne_Update_Runner_bleibt_der_Update_Button_verborgen()
    {
        // Browser-Head: kein Self-Update, also auch kein toter Button.
        var vm = new InfoViewModel();

        Assert.False(vm.CanCheckForUpdates);
        Assert.False(vm.CheckForUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public void Mit_Update_Runner_ist_der_Button_aktiv()
    {
        var vm = new InfoViewModel(_ => Task.CompletedTask);

        Assert.True(vm.CanCheckForUpdates);
        Assert.True(vm.CheckForUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Update_Pruefung_erzwingt_den_Check()
    {
        // force: true — die Intervall-Sperre des Auto-Checks darf nicht greifen, wenn der
        // Nutzer selbst auf den Knopf drückt.
        bool? forced = null;
        var vm = new InfoViewModel(f => { forced = f; return Task.CompletedTask; });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(forced);
    }

    [Fact]
    public async Task Waehrend_der_Pruefung_ist_der_Button_gesperrt()
    {
        // Ohne NotifyCanExecuteChangedFor am IsCheckingForUpdates-Flag bliebe der Button
        // aktiv und der Nutzer könnte den Check mehrfach parallel starten.
        var gate = new TaskCompletionSource();
        var vm = new InfoViewModel(_ => gate.Task);

        var running = vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(vm.IsCheckingForUpdates);
        Assert.False(vm.CheckForUpdatesCommand.CanExecute(null));

        gate.SetResult();
        await running;

        Assert.False(vm.IsCheckingForUpdates);
        Assert.True(vm.CheckForUpdatesCommand.CanExecute(null));
    }
}
