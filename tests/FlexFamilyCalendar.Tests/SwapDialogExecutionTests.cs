using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.ViewModels;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Der Tausch-Dialog führt seine Aktion aus, solange er offen ist, und zeigt einen Fehlschlag an.
/// Vorher schloss er zuerst; scheiterte das Annehmen danach, stand die Warnung für Millisekunden
/// in der Statuszeile und wurde vom Neuladen der Woche überschrieben — für den Nutzer passierte
/// einfach nichts.
/// </summary>
[Collection("Localizer")]
public class SwapDialogExecutionTests
{
    private static readonly DateOnly Day = new(2026, 9, 22);

    private static User Anna() => new() { Id = "anna", Username = "anna", DisplayName = "Anna", Category = PersonCategory.Employee };
    private static User Bert() => new() { Id = "bert", Username = "bert", DisplayName = "Bert", Category = PersonCategory.Employee };

    private static ShiftSwapRequest Pending() => new()
    {
        Id = "s1", Mode = SwapMode.GiveAway,
        FromUserId = "anna", FromUserName = "Anna", FromDate = Day.ToString("yyyy-MM-dd"), FromEntryId = "e1",
        ToUserId = "bert", ToUserName = "Bert"
    };

    // ───────── Dialog ─────────

    [Fact]
    public async Task Failed_action_keeps_the_dialog_open_and_shows_why()
    {
        SwapDialogResult? closedWith = null;
        var closed = false;
        var vm = new ShiftSwapViewModel(Bert(), Pending(), SwapDialogMode.Respond, "…",
            _ => Task.FromResult<string?>("Zeitliche Überschneidung"));
        vm.Closed += r => { closed = true; closedWith = r; };

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Null(closedWith);
        Assert.Equal("Zeitliche Überschneidung", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Successful_action_closes_the_dialog()
    {
        SwapDialogResult? closedWith = null;
        var vm = new ShiftSwapViewModel(Bert(), Pending(), SwapDialogMode.Respond, "…",
            _ => Task.FromResult<string?>(null));
        vm.Closed += r => closedWith = r;

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.NotNull(closedWith);
        Assert.Equal(SwapDialogAction.Accept, closedWith!.Action);
    }

    [Fact]
    public async Task Retry_after_a_failure_clears_the_old_message()
    {
        var attempts = 0;
        var vm = new ShiftSwapViewModel(Bert(), Pending(), SwapDialogMode.Respond, "…",
            _ => Task.FromResult<string?>(++attempts == 1 ? "erster Versuch schief" : null));
        var closed = false;
        vm.Closed += _ => closed = true;

        await vm.AcceptCommand.ExecuteAsync(null);
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.Equal("", vm.ErrorMessage);
    }

    [Fact]
    public async Task Knöpfe_sind_waehrend_der_Ausfuehrung_gesperrt()
    {
        var gate = new TaskCompletionSource<string?>();
        var vm = new ShiftSwapViewModel(Bert(), Pending(), SwapDialogMode.Respond, "…", _ => gate.Task);

        var running = vm.AcceptCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);

        gate.SetResult(null);
        await running;
        Assert.False(vm.IsBusy);
    }

    // ───────── Ganzer Weg über das Kalender-ViewModel (lokaler Modus) ─────────

    private static async Task<(CalendarViewModel Vm, InMemoryStorageService Storage)> CalendarAsync(
        bool bertAlreadyWorks, bool finalized = false)
    {
        var storage = new InMemoryStorageService();
        await storage.SaveUsersAsync(new List<User> { Anna(), Bert() });
        var entries = new List<CalendarEntry>
        {
            new() { Id = "e1", UserId = "anna", UserDisplayName = "Anna", Type = EntryType.Work,
                    StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) }
        };
        if (bertAlreadyWorks)
            entries.Add(new() { Id = "e2", UserId = "bert", UserDisplayName = "Bert", Type = EntryType.Work,
                                StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(20, 0, 0) });
        await storage.SaveDayAsync(new CalendarDay
        {
            DateString = Day.ToString("yyyy-MM-dd"), IsFinalized = finalized, Entries = entries
        });
        storage.SeedSwapRequests(new[] { Pending() });

        var vm = new CalendarViewModel(storage, Bert(), new NotificationService(storage),
            new AiService(Array.Empty<IAiProvider>()), new LocalMailSender(storage));
        await vm.RefreshAllAsync(silent: true);
        return (vm, storage);
    }

    [Fact]
    public async Task Overlap_comes_back_as_a_readable_message_and_moves_nothing()
    {
        var (vm, storage) = await CalendarAsync(bertAlreadyWorks: true);

        var error = await vm.ExecuteSwapAsync(new SwapDialogResult(SwapDialogAction.Accept, Pending()));

        Assert.Equal(Localizer.Instance["Swap_ErrorOverlap"], error);
        Assert.Equal("anna", (await storage.LoadDayAsync(Day)).Entries.Single(e => e.Id == "e1").UserId);
    }

    [Fact]
    public async Task Finalized_day_comes_back_as_a_readable_message()
    {
        var (vm, _) = await CalendarAsync(bertAlreadyWorks: false, finalized: true);

        var error = await vm.ExecuteSwapAsync(new SwapDialogResult(SwapDialogAction.Accept, Pending()));

        Assert.Equal(Localizer.Instance["Swap_ErrorFinalized"], error);
    }

    [Fact]
    public async Task Accepting_moves_the_shift_and_tells_the_offerer()
    {
        var (vm, storage) = await CalendarAsync(bertAlreadyWorks: false);

        var error = await vm.ExecuteSwapAsync(new SwapDialogResult(SwapDialogAction.Accept, Pending()));

        Assert.Null(error);
        Assert.Equal("bert", (await storage.LoadDayAsync(Day)).Entries.Single().UserId);
        Assert.Single(await storage.LoadNotificationsAsync("anna"), n => n.MessageKey == "Notif_SwapAccepted");
    }

    [Fact]
    public async Task Rejecting_a_settled_swap_shows_the_reason_instead_of_crashing()
    {
        var (vm, storage) = await CalendarAsync(bertAlreadyWorks: false);
        await storage.RejectSwapRequestAsync("s1");

        var error = await vm.ExecuteSwapAsync(new SwapDialogResult(SwapDialogAction.Reject, Pending()));

        Assert.Equal(Localizer.Instance["Swap_ErrorNotPending"], error);
    }
}
