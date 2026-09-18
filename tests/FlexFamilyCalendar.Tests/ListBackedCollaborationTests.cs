using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Schichttausch und Benachrichtigungen im lokalen Modus (JSON-Dateien). Der Server-Modus hat
/// dieselben Regeln serverseitig — siehe SwapEndpointTests/NotificationEndpointTests.
/// </summary>
[Collection("Localizer")]   // Close übersetzt seine Fehlermeldung über den Localizer
public class ListBackedCollaborationTests
{
    private static readonly DateOnly Day = new(2026, 9, 21);

    private static async Task<(InMemoryStorageService Storage, CalendarEntry Shift)> WithShiftAsync()
    {
        var storage = new InMemoryStorageService();
        var shift = new CalendarEntry
        {
            Id = "e1", UserId = "anna", UserDisplayName = "Anna", Type = EntryType.Work,
            StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0)
        };
        await storage.SaveDayAsync(new CalendarDay { DateString = Day.ToString("yyyy-MM-dd"), Entries = { shift } });
        return (storage, shift);
    }

    private static ShiftSwapRequest GiveAway(string entryId) => new()
    {
        Mode = SwapMode.GiveAway,
        FromUserId = "anna", FromUserName = "Anna", FromDate = Day.ToString("yyyy-MM-dd"), FromEntryId = entryId,
        ToUserId = "bert", ToUserName = "Bert"
    };

    [Fact]
    public async Task Accept_moves_the_shift_and_settles_the_request()
    {
        var (storage, shift) = await WithShiftAsync();
        var swap = await storage.CreateSwapRequestAsync(GiveAway(shift.Id));

        var error = await storage.AcceptSwapRequestAsync(swap);

        Assert.Null(error);
        Assert.Equal("bert", (await storage.LoadDayAsync(Day)).Entries.Single().UserId);
        Assert.Equal(SwapStatus.Accepted, (await storage.LoadSwapRequestsAsync()).Single().Status);
    }

    [Fact]
    public async Task Accepting_twice_is_refused_and_moves_nothing_more()
    {
        var (storage, shift) = await WithShiftAsync();
        var swap = await storage.CreateSwapRequestAsync(GiveAway(shift.Id));
        await storage.AcceptSwapRequestAsync(swap);

        var error = await storage.AcceptSwapRequestAsync(swap);

        Assert.Equal("Swap_ErrorNotPending", error);
    }

    [Fact]
    public async Task Stale_request_is_refused()
    {
        var (storage, _) = await WithShiftAsync();
        var swap = await storage.CreateSwapRequestAsync(GiveAway("gibt-es-nicht"));

        Assert.Equal("Swap_ErrorStale", await storage.AcceptSwapRequestAsync(swap));
    }

    [Fact]
    public async Task Create_ignores_a_status_the_caller_made_up()
    {
        var (storage, shift) = await WithShiftAsync();
        var sneaky = GiveAway(shift.Id);
        sneaky.Status = SwapStatus.Accepted;

        var created = await storage.CreateSwapRequestAsync(sneaky);

        Assert.Equal(SwapStatus.Pending, created.Status);
    }

    [Fact]
    public async Task Reject_on_a_settled_request_throws()
    {
        var (storage, shift) = await WithShiftAsync();
        var swap = await storage.CreateSwapRequestAsync(GiveAway(shift.Id));
        await storage.RejectSwapRequestAsync(swap.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.WithdrawSwapRequestAsync(swap.Id));
    }

    [Fact]
    public async Task Notifications_are_read_per_recipient()
    {
        var storage = new InMemoryStorageService();
        await storage.AddNotificationsAsync(new[]
        {
            new Notification { UserId = "admin", MessageKey = "Notif_SickReported", Args = { "Mara", "21.09." } },
            new Notification { UserId = "tim", MessageKey = "Notif_SwapOffered" }
        });

        var forTim = await storage.LoadNotificationsAsync("tim");

        Assert.Single(forTim);
        Assert.DoesNotContain(forTim, n => n.MessageKey == "Notif_SickReported");
    }

    [Fact]
    public async Task Mark_all_read_only_touches_the_given_recipient()
    {
        var storage = new InMemoryStorageService();
        await storage.AddNotificationsAsync(new[]
        {
            new Notification { UserId = "a", MessageKey = "K" },
            new Notification { UserId = "b", MessageKey = "K" }
        });

        await storage.MarkNotificationsReadAsync("a", null);

        Assert.True(storage.AllNotifications.Single(n => n.UserId == "a").IsRead);
        Assert.False(storage.AllNotifications.Single(n => n.UserId == "b").IsRead);
    }

    [Fact]
    public async Task Mark_read_by_id_cannot_reach_someone_elses_notification()
    {
        var storage = new InMemoryStorageService();
        var foreign = new Notification { UserId = "b", MessageKey = "K" };
        await storage.AddNotificationsAsync(new[] { foreign });

        await storage.MarkNotificationsReadAsync("a", new[] { foreign.Id });

        Assert.False(storage.AllNotifications.Single().IsRead);
    }
}
