using FlexFamilyCalendar.Services;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class NotificationServiceTests
{
    private static NotificationService NewService() => new(new InMemoryStorageService());

    [Fact]
    public async Task Add_StoresForRecipient_Unread()
    {
        var svc = NewService();
        await svc.AddAsync("u1", "Notif_SwapOffered", "2026-06-02", "Anna", "02.06.2026");

        var list = await svc.GetForUserAsync("u1");
        Assert.Single(list);
        Assert.Equal("Notif_SwapOffered", list[0].MessageKey);
        Assert.Equal(new[] { "Anna", "02.06.2026" }, list[0].Args);
        Assert.Equal("2026-06-02", list[0].RelatedDate);
        Assert.False(list[0].IsRead);
    }

    [Fact]
    public async Task GetForUser_FiltersByRecipient_NewestFirst()
    {
        var svc = NewService();
        await svc.AddAsync("u1", "A", null);
        await Task.Delay(5);
        await svc.AddAsync("u1", "B", null);
        await svc.AddAsync("u2", "C", null);

        var u1 = await svc.GetForUserAsync("u1");
        Assert.Equal(2, u1.Count);
        Assert.Equal("B", u1[0].MessageKey);   // neueste zuerst
        Assert.Equal("A", u1[1].MessageKey);
        Assert.Single(await svc.GetForUserAsync("u2"));
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyUnreadForUser()
    {
        var svc = NewService();
        await svc.AddAsync("u1", "A", null);
        await svc.AddAsync("u1", "B", null);
        await svc.AddAsync("u2", "C", null);

        Assert.Equal(2, await svc.UnreadCountAsync("u1"));

        var first = (await svc.GetForUserAsync("u1")).Last();
        await svc.MarkReadAsync(first.Id);
        Assert.Equal(1, await svc.UnreadCountAsync("u1"));
    }

    [Fact]
    public async Task MarkAllRead_OnlyAffectsThatUser()
    {
        var svc = NewService();
        await svc.AddAsync("u1", "A", null);
        await svc.AddAsync("u1", "B", null);
        await svc.AddAsync("u2", "C", null);

        await svc.MarkAllReadAsync("u1");

        Assert.Equal(0, await svc.UnreadCountAsync("u1"));
        Assert.Equal(1, await svc.UnreadCountAsync("u2"));
    }

    [Fact]
    public async Task AddMany_CreatesOnePerDistinctRecipient()
    {
        var svc = NewService();
        await svc.AddManyAsync(new[] { "u1", "u2", "u2", "" }, "Notif_WeekFinalized", "2026-06-01", "22", "2026");

        Assert.Equal(1, await svc.UnreadCountAsync("u1"));
        Assert.Equal(1, await svc.UnreadCountAsync("u2"));
    }

    [Fact]
    public async Task Add_EmptyUser_Ignored()
    {
        var svc = NewService();
        await svc.AddAsync("", "A", null);
        Assert.Empty(await svc.GetForUserAsync(""));
    }
}
