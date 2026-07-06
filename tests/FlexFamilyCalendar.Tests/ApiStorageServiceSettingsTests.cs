using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

// Regression für den 401-Login-Screen-Bug: Ohne Token darf ApiStorageService.LoadSettingsAsync
// NICHT auf den Server gehen — sonst blockt das den Login-View (kein Token vor dem Login).
// Wir zwingen die Fehlgeschlagene-URL (localhost:1) — sobald ein Request rausgeht, würde der
// Test lange hängen oder werfen. Ohne Token kommt der Server-Call gar nicht erst zustande.
public class ApiStorageServiceSettingsTests
{
    private static ApiStorageService NewService()
    {
        // Bewusst eine URL, die zuverlässig fehlschlägt — falls die Guard-Klausel weg wäre,
        // würde LoadSettingsAsync einen Connection-Error werfen (oder timeouten).
        var api = new ApiClient("http://127.0.0.1:1");
        var localStore = new BrowserSettingsStorage(new InMemoryBrowserKeyValueStore());
        return new ApiStorageService(api, localStore);
    }

    [Fact]
    public async Task LoadSettingsAsync_ohne_Token_überspringt_Server_und_liefert_Defaults()
    {
        var svc = NewService();
        // Timeout als zusätzliches Netz: 3 s reichen locker für den lokalen Store,
        // ein echter TCP-Connect-Retry auf 127.0.0.1:1 würde das reißen.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var task = svc.LoadSettingsAsync();
        var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
        Assert.Same(task, completed);

        var settings = await task;
        Assert.Equal("BY", settings.HolidayState);           // Default aus AppSettings
        Assert.Equal(2.0, settings.OvernightHoursPerDay);    // Default aus AppSettings
    }

    [Fact]
    public async Task SaveSettingsAsync_ohne_Token_schreibt_lokal_und_überspringt_Server()
    {
        var svc = NewService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var task = svc.SaveSettingsAsync(new Models.AppSettings { HolidayState = "NW", OvernightHoursPerDay = 3 });
        var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
        Assert.Same(task, completed);
        await task;   // wirft nicht

        // Local store hat die neuen Werte.
        var reloaded = await svc.LoadSettingsAsync();
        Assert.Equal("NW", reloaded.HolidayState);
        Assert.Equal(3, reloaded.OvernightHoursPerDay);
    }
}
