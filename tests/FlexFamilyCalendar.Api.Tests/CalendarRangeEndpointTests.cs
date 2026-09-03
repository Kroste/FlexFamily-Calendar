using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Entries;
using FlexFamilyCalendar.Api.DayNotes;
using FlexFamilyCalendar.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>
/// Bereichs-Abrufe für die Wochenansicht: alle Einträge und alle Tagesnotizen einer Woche in je
/// einer Anfrage statt sieben. Enthält die Regressionsprobe dafür, dass die Sichtbarkeit einer
/// mehrtägigen Abwesenheit nicht mehr davon abhängt, welchen Ausschnitt der Client anfragt.
/// </summary>
public class CalendarRangeEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private static readonly DateOnly Monday = new(2026, 6, 1);

    public CalendarRangeEndpointTests(ApiTestFactory factory) => _factory = factory;

    private Guid SeedOtherPersonsAbsence(bool finalizeMonday)
    {
        var id = Guid.NewGuid();
        _factory.Seed(db =>
        {
            var admin = db.Users.Single(u => u.Username == ApiTestFactory.AdminUser);
            db.Entries.Add(new CalendarEntry
            {
                Id = id,
                UserId = admin.Id,                 // fremde Person aus Sicht des Plain-Users
                Type = EntryTypes.Vacation,
                Date = Monday,
                EndDate = Monday.AddDays(4),
                Note = "Malle",
                Status = EntryStatus.Approved,
                CreatedBy = admin.Id
            });
            if (finalizeMonday && !db.DayMeta.Any(m => m.Date == Monday))
                db.DayMeta.Add(new CalendarDayMeta { Date = Monday, Note = "", IsFinalized = true });
        });
        return id;
    }

    private async Task<List<EntryDto>> GetEntriesAsync(HttpClient client, DateOnly from, DateOnly to)
        => await client.GetFromJsonAsync<List<EntryDto>>(
               $"api/entries?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
               TestContext.Current.CancellationToken) ?? new();

    [Fact]
    public async Task Multi_day_absence_stays_visible_on_every_day_it_covers()
    {
        var id = SeedOtherPersonsAbsence(finalizeMonday: true);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        // Mittwoch einzeln abgefragt — der Starttag (Montag) liegt außerhalb des Fensters.
        // Genau hier verschwand die Abwesenheit vorher aus der Kollegensicht.
        var wednesday = await GetEntriesAsync(client, Monday.AddDays(2), Monday.AddDays(2));

        var dto = Assert.Single(wednesday, e => e.Id == id);
        Assert.True(dto.Masked);                       // fremder Urlaub → nur „Abwesend"
        Assert.Equal(EntryTypes.Absence, dto.Type);
        Assert.Null(dto.Note);
    }

    [Fact]
    public async Task Multi_day_absence_stays_hidden_while_the_day_is_not_finalized()
    {
        var id = SeedOtherPersonsAbsence(finalizeMonday: false);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var wednesday = await GetEntriesAsync(client, Monday.AddDays(2), Monday.AddDays(2));

        Assert.DoesNotContain(wednesday, e => e.Id == id);
    }

    [Fact]
    public async Task Week_range_returns_the_same_entry_as_the_single_day_query()
    {
        var id = SeedOtherPersonsAbsence(finalizeMonday: true);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var week = await GetEntriesAsync(client, Monday, Monday.AddDays(6));
        var single = await GetEntriesAsync(client, Monday.AddDays(3), Monday.AddDays(3));

        Assert.Contains(week, e => e.Id == id);
        Assert.Contains(single, e => e.Id == id);
    }

    [Fact]
    public async Task Day_notes_range_returns_the_whole_week_in_one_call()
    {
        _factory.Seed(db =>
        {
            if (!db.DayMeta.Any(m => m.Date == Monday.AddDays(8)))
                db.DayMeta.Add(new CalendarDayMeta { Date = Monday.AddDays(8), Note = "Elternabend", IsFinalized = true });
            if (!db.DayMeta.Any(m => m.Date == Monday.AddDays(9)))
                db.DayMeta.Add(new CalendarDayMeta { Date = Monday.AddDays(9), Note = "", IsFinalized = true });
        });
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var notes = await client.GetFromJsonAsync<List<DayNoteRangeDto>>(
            $"api/day-notes?from={Monday.AddDays(7):yyyy-MM-dd}&to={Monday.AddDays(13):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(notes);
        var withNote = Assert.Single(notes!, n => n.Date == Monday.AddDays(8));
        Assert.Equal("Elternabend", withNote.Note);
        Assert.True(withNote.IsFinalized);
        // Tage ohne Zeile fehlen bewusst — der Client ergänzt sie als leer/nicht finalisiert.
        Assert.DoesNotContain(notes!, n => n.Date == Monday.AddDays(10));
    }

    [Fact]
    public async Task Day_notes_range_refuses_an_absurd_span()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var resp = await client.GetAsync("api/day-notes?from=2000-01-01&to=2030-01-01",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Day_notes_range_needs_authentication()
    {
        var client = _factory.CreateSeededClient();

        var resp = await client.GetAsync($"api/day-notes?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
