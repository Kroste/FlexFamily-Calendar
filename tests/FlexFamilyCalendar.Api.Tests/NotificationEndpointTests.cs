using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Models;
using FlexFamilyCalendar.Api.Notifications;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>Regeln fürs Anlegen, ohne Server.</summary>
public class NotificationRulesTests
{
    private static CreateNotificationRequest Req(string key, string? action = null, List<string>? args = null)
        => new(Guid.NewGuid().ToString(), key, args, null, action, null);

    [Theory]
    [InlineData("Notif_SwapOffered")]
    [InlineData("Notif_SwapAccepted")]
    [InlineData("Notif_SwapRejected")]
    [InlineData("Notif_SwapWithdrawn")]
    [InlineData("Notif_SickReported")]
    public void Everyone_may_send_the_messages_of_their_own_flows(string key)
        => Assert.Null(NotificationRules.CheckCreate(Req(key), isAdmin: false));

    [Theory]
    [InlineData("Notif_ShiftRemoved")]
    [InlineData("Notif_ShiftChanged")]
    [InlineData("Notif_ShiftAssigned")]
    [InlineData("Notif_WeekFinalized")]
    public void Planning_messages_are_admin_only(string key)
    {
        // Sonst könnte ein Mitarbeiter den Kollegen „Deine Schicht wurde entfernt" vortäuschen.
        Assert.NotNull(NotificationRules.CheckCreate(Req(key), isAdmin: false));
        Assert.Null(NotificationRules.CheckCreate(Req(key), isAdmin: true));
    }

    [Fact]
    public void ReplanSick_only_travels_with_a_sick_report()
    {
        Assert.Null(NotificationRules.CheckCreate(Req("Notif_SickReported", "ReplanSick"), isAdmin: false));
        Assert.NotNull(NotificationRules.CheckCreate(Req("Notif_SwapOffered", "ReplanSick"), isAdmin: false));
        Assert.NotNull(NotificationRules.CheckCreate(Req("Notif_SickReported", "DeleteEverything"), isAdmin: true));
    }

    [Fact]
    public void Unknown_keys_and_oversized_args_are_refused()
    {
        Assert.NotNull(NotificationRules.CheckCreate(Req("Hallo"), isAdmin: true));
        Assert.NotNull(NotificationRules.CheckCreate(
            Req("Notif_SwapOffered", args: Enumerable.Repeat("x", NotificationRules.MaxArgs + 1).ToList()), isAdmin: false));
        Assert.NotNull(NotificationRules.CheckCreate(
            Req("Notif_SwapOffered", args: new() { new string('x', NotificationRules.MaxArgLength + 1) }), isAdmin: false));
    }
}

/// <summary>Die Endpunkte mit mehreren echten Nutzern. Jeder Test legt eigene Nutzer an —
/// die Fixture teilt die DB über die ganze Klasse.</summary>
public class NotificationEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public NotificationEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(Guid Id, HttpClient Client)> NewUserAsync(string role = "User")
    {
        var name = "n-" + Guid.NewGuid().ToString("N")[..10];
        var id = Guid.NewGuid();
        _factory.Seed(db => db.Users.Add(new UserEntity
        {
            Id = id, Username = name, DisplayName = name, Role = role, Category = "Employee",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw")
        }));
        return (id, await _factory.CreateAuthenticatedClientAsync(name, "pw"));
    }

    private void SeedNotification(Guid recipient, string key, params string[] args)
        => _factory.Seed(db => db.Notifications.Add(new NotificationEntity
        {
            UserId = recipient.ToString(), CreatedAt = DateTime.UtcNow.ToString("o"),
            MessageKey = key, Args = args.ToList()
        }));

    private static async Task<List<NotificationDto>> ReadAsync(HttpClient c)
        => await c.GetFromJsonAsync<List<NotificationDto>>("api/notifications", Ct) ?? new();

    [Fact]
    public async Task Nobody_reads_someone_elses_sick_report()
    {
        var (adminId, _) = await NewUserAsync("Admin");
        var (_, colleague) = await NewUserAsync();
        SeedNotification(adminId, "Notif_SickReported", "Mara", "18.09.2026");

        var seen = await ReadAsync(colleague);

        // Vorher kam hier die komplette Tabelle an — Krankmeldungen der Kollegen im Klartext.
        Assert.DoesNotContain(seen, n => n.MessageKey == "Notif_SickReported");
        Assert.All(seen, n => Assert.NotEqual(adminId.ToString(), n.UserId));
    }

    [Fact]
    public async Task Recipient_reads_their_own()
    {
        var (me, client) = await NewUserAsync();
        SeedNotification(me, "Notif_SwapOffered", "Tim", "18.09.2026");

        var seen = await ReadAsync(client);

        var n = Assert.Single(seen);
        Assert.Equal("Notif_SwapOffered", n.MessageKey);
        Assert.False(n.IsRead);
    }

    [Fact]
    public async Task Colleague_may_offer_a_swap_to_someone_else()
    {
        var (_, sender) = await NewUserAsync();
        var (recipientId, recipient) = await NewUserAsync();

        var resp = await sender.PostAsJsonAsync("api/notifications", new[]
        {
            new CreateNotificationRequest(recipientId.ToString(), "Notif_SwapOffered", new() { "Tim", "18.09." }, "2026-09-18", null, null)
        }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Single(await ReadAsync(recipient), n => n.MessageKey == "Notif_SwapOffered");
    }

    [Fact]
    public async Task Colleague_cannot_fake_a_planning_message()
    {
        var (_, sender) = await NewUserAsync();
        var (recipientId, recipient) = await NewUserAsync();

        var resp = await sender.PostAsJsonAsync("api/notifications", new[]
        {
            new CreateNotificationRequest(recipientId.ToString(), "Notif_ShiftRemoved", new() { "18.09." }, null, null, null)
        }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Empty(await ReadAsync(recipient));
    }

    [Fact]
    public async Task Unknown_recipient_is_refused()
    {
        var (_, sender) = await NewUserAsync();

        var resp = await sender.PostAsJsonAsync("api/notifications", new[]
        {
            new CreateNotificationRequest(Guid.NewGuid().ToString(), "Notif_SwapOffered", null, null, null, null)
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Mark_read_only_touches_your_own()
    {
        var (me, client) = await NewUserAsync();
        var (otherId, other) = await NewUserAsync();
        SeedNotification(me, "Notif_SwapOffered", "a", "b");
        SeedNotification(otherId, "Notif_SwapOffered", "a", "b");

        // „Alle als gelesen" — ohne Ids.
        var resp = await client.PostAsJsonAsync("api/notifications/read", new MarkNotificationsReadRequest(null), Ct);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.All(await ReadAsync(client), n => Assert.True(n.IsRead));
        Assert.All(await ReadAsync(other), n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task Mark_read_with_someone_elses_id_changes_nothing()
    {
        var (_, client) = await NewUserAsync();
        var (otherId, other) = await NewUserAsync();
        SeedNotification(otherId, "Notif_SwapOffered", "a", "b");
        var foreignId = (await ReadAsync(other)).Single().Id;

        await client.PostAsJsonAsync("api/notifications/read", new MarkNotificationsReadRequest(new() { foreignId }), Ct);

        Assert.False((await ReadAsync(other)).Single().IsRead);
    }

    [Fact]
    public async Task Replace_all_is_gone()
    {
        // Der alte PUT löschte die ganze Tabelle und schrieb sie neu — für jeden Angemeldeten.
        var (_, client) = await NewUserAsync();

        var resp = await client.PutAsJsonAsync("api/notifications", Array.Empty<object>(), Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }
}
