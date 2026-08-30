using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.ActivityTypes;
using FlexFamilyCalendar.Api.Models;
using FlexFamilyCalendar.Api.Notifications;
using FlexFamilyCalendar.Api.PlannerNotes;
using FlexFamilyCalendar.Api.RecurringActivities;
using FlexFamilyCalendar.Api.Swaps;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>
/// Lesepfade der Listen-Endpunkte. Sie waren bisher ungetestet — aufgefallen, als die
/// GET-Abfragen auf <c>AsNoTracking</c> umgestellt wurden und es dafür keine Absicherung gab.
///
/// <para>Die Testdaten kommen über den DbContext statt über die PUT-Endpunkte: die ersetzen die
/// ganze Liste per <c>ExecuteDeleteAsync</c>, was der InMemory-Provider nicht unterstützt und
/// im Test mit 500 endet. Das ist der Grund, warum es hier nie Tests gab.</para>
///
/// <para>Der interessanteste Fall ist <c>recurring-activities</c>: dort hängt an jeder Aktivität
/// eine Liste von Aussetzungen, und ohne Change-Tracking materialisiert EF
/// Navigationseigenschaften anders. Deshalb prüft der Test ausdrücklich, dass die
/// Kind-Datensätze vollständig zurückkommen.</para>
/// </summary>
public class ListEndpointRoundTripTests
{
    [Fact]
    public async Task ActivityTypes_werden_nach_Namen_sortiert_geliefert()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db => db.ActivityTypes.AddRange(
            new ActivityTypeEntity { Name = "Sport", Color = "#27AE60", Categories = ["Child"] },
            new ActivityTypeEntity { Name = "Arzt", Color = "#C0392B", Categories = ["Parent", "Child"] }));

        var read = await client.GetFromJsonAsync<List<ActivityTypeDto>>(
            "api/activity-types", TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(2, read!.Count);
        Assert.Equal("Arzt", read[0].Name);
        Assert.Equal("Sport", read[1].Name);
        // Die Kategorienliste ist ein text[] in Postgres — sie darf beim Lesen nicht leer werden.
        Assert.Equal(["Parent", "Child"], read[0].Categories);
    }

    [Fact]
    public async Task RecurringActivities_liefern_ihre_Aussetzungen_mit()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db => db.RecurringActivities.Add(new RecurringActivityEntity
        {
            UserId = "u1",
            UserDisplayName = "Mia",
            Title = "Fußball",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(17, 0),
            Weekdays = [(int)DayOfWeek.Thursday],
            Skips =
            [
                new RecurrenceSkipEntity { From = new DateOnly(2026, 7, 1), To = new DateOnly(2026, 7, 14), Reason = "Urlaub" },
                new RecurrenceSkipEntity { From = new DateOnly(2026, 10, 1), To = new DateOnly(2026, 10, 7) },
            ],
        }));

        var read = await client.GetFromJsonAsync<List<RecurringActivityDto>>(
            "api/recurring-activities", TestContext.Current.CancellationToken);

        var activity = Assert.Single(read!);
        Assert.Equal("Fußball", activity.Title);
        Assert.Equal([(int)DayOfWeek.Thursday], activity.Weekdays);

        // Der Kern: die Kind-Datensätze dürfen ohne Tracking nicht verlorengehen.
        Assert.Equal(2, activity.Skips.Count);
        Assert.Contains(activity.Skips, s => s.Reason == "Urlaub" && s.From == new DateOnly(2026, 7, 1));
        Assert.Contains(activity.Skips, s => s.Reason is null && s.To == new DateOnly(2026, 10, 7));
    }

    [Fact]
    public async Task PlannerNotes_kommen_in_Erstellungsreihenfolge()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db => db.PlannerNotes.AddRange(
            new PlannerNoteEntity { Text = "später notiert", CreatedAtUtc = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc) },
            new PlannerNoteEntity { Text = "zuerst notiert", CreatedAtUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc) }));

        var read = await client.GetFromJsonAsync<List<PlannerNoteDto>>(
            "api/planner-notes", TestContext.Current.CancellationToken);

        Assert.Equal(2, read!.Count);
        Assert.Equal("zuerst notiert", read[0].Text);
    }

    [Fact]
    public async Task SwapRequests_kommen_vollstaendig_zurueck()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db => db.SwapRequests.Add(new ShiftSwapRequestEntity
        {
            CreatedAt = "2026-08-01T10:00:00Z",
            Status = 0,
            Mode = 1,
            FromUserId = "u1", FromUserName = "Anna", FromDate = "2026-08-03", FromEntryId = "e1",
            ToUserId = "u2", ToUserName = "Bert", ToDate = "2026-08-04", ToEntryId = "e2",
            Message = "Bitte tauschen",
        }));

        var read = await client.GetFromJsonAsync<List<ShiftSwapRequestDto>>(
            "api/swap-requests", TestContext.Current.CancellationToken);

        var swap = Assert.Single(read!);
        Assert.Equal("Anna", swap.FromUserName);
        Assert.Equal("Bert", swap.ToUserName);
        Assert.Equal("Bitte tauschen", swap.Message);
    }

    [Fact]
    public async Task Notifications_behalten_ihre_Argumentliste()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db => db.Notifications.Add(new NotificationEntity
        {
            UserId = "u1",
            CreatedAt = "2026-08-01T10:00:00Z",
            MessageKey = "Notif_VacationRequested",
            Args = ["Anna", "12.08."],
            RelatedDate = "2026-08-12",
            Action = "approve",
            RelatedUserId = "u2",
        }));

        var read = await client.GetFromJsonAsync<List<NotificationDto>>(
            "api/notifications", TestContext.Current.CancellationToken);

        var n = Assert.Single(read!);
        Assert.Equal("Notif_VacationRequested", n.MessageKey);
        Assert.Equal(["Anna", "12.08."], n.Args);
        Assert.Equal("approve", n.Action);
    }

    [Fact]
    public async Task Leere_Listen_liefern_leere_Antworten_statt_Fehler()
    {
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        Assert.Empty((await client.GetFromJsonAsync<List<ActivityTypeDto>>(
            "api/activity-types", TestContext.Current.CancellationToken))!);
        Assert.Empty((await client.GetFromJsonAsync<List<RecurringActivityDto>>(
            "api/recurring-activities", TestContext.Current.CancellationToken))!);
        Assert.Empty((await client.GetFromJsonAsync<List<ShiftSwapRequestDto>>(
            "api/swap-requests", TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Listen_Endpunkte_verlangen_eine_Anmeldung()
    {
        using var factory = new ApiTestFactory();
        var anonym = factory.CreateSeededClient();

        foreach (var pfad in new[] { "api/activity-types", "api/recurring-activities",
                                     "api/swap-requests", "api/notifications" })
        {
            var resp = await anonym.GetAsync(pfad, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
    }

    [Fact]
    public async Task ChatVerlauf_bleibt_pro_Benutzer_getrennt()
    {
        // Datenschutz-Grenze: der Endpunkt filtert nach der Benutzer-ID aus dem Token.
        using var factory = new ApiTestFactory();
        var client = await factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        factory.Seed(db =>
        {
            var adminId = db.Users.First(u => u.Username == ApiTestFactory.AdminUser).Id;
            db.ChatHistory.Add(new ChatHistoryEntity
            {
                UserId = adminId, Role = "user", Text = "meine Frage",
                CreatedAtUtc = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            });
            db.ChatHistory.Add(new ChatHistoryEntity
            {
                UserId = Guid.NewGuid(), Role = "user", Text = "fremde Frage",
                CreatedAtUtc = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            });
        });

        var read = await client.GetFromJsonAsync<List<ChatHistory.ChatHistoryDto>>(
            "api/chat-history", TestContext.Current.CancellationToken);

        var eintrag = Assert.Single(read!);
        Assert.Equal("meine Frage", eintrag.Text);
    }
}
