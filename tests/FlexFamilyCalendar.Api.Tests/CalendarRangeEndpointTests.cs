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

    public CalendarRangeEndpointTests(ApiTestFactory factory) => _factory = factory;

    /// <summary>
    /// Jeder Test bekommt seine EIGENE Woche. Die Fixture teilt eine DB über die ganze Klasse,
    /// und xunit.v3 legt die Reihenfolge innerhalb einer Klasse nicht fest — mit einem
    /// gemeinsamen Montag finalisierte ein Test den Tag, den der nächste unfinalisiert braucht.
    /// Lokal ging das gut, auf dem CI-Runner nicht.
    /// </summary>
    private static DateOnly WeekOf(int index) => new DateOnly(2026, 6, 1).AddDays(index * 28);

    private Guid SeedOtherPersonsAbsence(DateOnly monday, bool finalizeMonday)
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
                Date = monday,
                EndDate = monday.AddDays(4),
                Note = "Malle",
                Status = EntryStatus.Approved,
                CreatedBy = admin.Id
            });
            if (finalizeMonday && !db.DayMeta.Any(m => m.Date == monday))
                db.DayMeta.Add(new CalendarDayMeta { Date = monday, Note = "", IsFinalized = true });
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
        var monday = WeekOf(0);
        var id = SeedOtherPersonsAbsence(monday, finalizeMonday: true);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        // Mittwoch einzeln abgefragt — der Starttag (Montag) liegt außerhalb des Fensters.
        // Genau hier verschwand die Abwesenheit vorher aus der Kollegensicht.
        var wednesday = await GetEntriesAsync(client, monday.AddDays(2), monday.AddDays(2));

        var dto = Assert.Single(wednesday, e => e.Id == id);
        Assert.True(dto.Masked);                       // fremder Urlaub → nur „Abwesend"
        Assert.Equal(EntryTypes.Absence, dto.Type);
        Assert.Null(dto.Note);
    }

    [Fact]
    public async Task Multi_day_absence_stays_hidden_while_the_day_is_not_finalized()
    {
        var monday = WeekOf(1);
        var id = SeedOtherPersonsAbsence(monday, finalizeMonday: false);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var wednesday = await GetEntriesAsync(client, monday.AddDays(2), monday.AddDays(2));

        Assert.DoesNotContain(wednesday, e => e.Id == id);
    }

    [Fact]
    public async Task Week_range_returns_the_same_entry_as_the_single_day_query()
    {
        var monday = WeekOf(2);
        var id = SeedOtherPersonsAbsence(monday, finalizeMonday: true);
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var week = await GetEntriesAsync(client, monday, monday.AddDays(6));
        var single = await GetEntriesAsync(client, monday.AddDays(3), monday.AddDays(3));

        Assert.Contains(week, e => e.Id == id);
        Assert.Contains(single, e => e.Id == id);
    }

    [Fact]
    public async Task Day_notes_range_returns_the_whole_week_in_one_call()
    {
        var monday = WeekOf(3);
        _factory.Seed(db =>
        {
            if (!db.DayMeta.Any(m => m.Date == monday.AddDays(1)))
                db.DayMeta.Add(new CalendarDayMeta { Date = monday.AddDays(1), Note = "Elternabend", IsFinalized = true });
        });
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var notes = await client.GetFromJsonAsync<List<DayNoteRangeDto>>(
            $"api/day-notes?from={monday:yyyy-MM-dd}&to={monday.AddDays(6):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(notes);
        var withNote = Assert.Single(notes!, n => n.Date == monday.AddDays(1));
        Assert.Equal("Elternabend", withNote.Note);
        Assert.True(withNote.IsFinalized);
        // Tage ohne Zeile fehlen bewusst — der Client ergänzt sie als leer/nicht finalisiert.
        Assert.DoesNotContain(notes!, n => n.Date == monday.AddDays(3));
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

        var monday = WeekOf(4);
        var resp = await client.GetAsync($"api/day-notes?from={monday:yyyy-MM-dd}&to={monday.AddDays(6):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
