using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.Api;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Server-Modus: ein Zeitraum ist auf dem Server EIN Eintrag, im Client einer pro Tag. Der
/// Abgleich beim Speichern eines Tages darf ihn weder je Tag neu anlegen noch löschen, nur weil
/// an einem Tag ein Gegenstück fehlt. Läuft gegen eine kleine Fake-API im Speicher.
/// </summary>
public class ApiSpanSyncTests
{
    private static readonly DateOnly Wed = new(2026, 9, 23);

    /// <summary>Die Einträge-Endpunkte, so wie der Server sie auslegt: Bereich überlappt das Fenster.</summary>
    private sealed class FakeEntriesApi : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
        public readonly List<ServerEntryDto> Entries = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.StartsWith("/api/day-notes/"))
                return Ok(new ServerDayNoteDto("", false));

            if (path == "/api/entries" && req.Method == HttpMethod.Get)
            {
                var q = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                var from = DateOnly.Parse(q["from"]!);
                var to = DateOnly.Parse(q["to"]!);
                return Ok(Entries.Where(e => e.Date <= to && (e.EndDate ?? e.Date) >= from).ToList());
            }
            if (path == "/api/entries" && req.Method == HttpMethod.Post)
            {
                var b = (await req.Content!.ReadFromJsonAsync<CreateEntryBody>(Json, ct))!;
                var dto = new ServerEntryDto(Guid.NewGuid().ToString(), b.UserId!, b.Type, b.Date, b.EndDate,
                    b.StartTime, b.EndTime, b.EndsNextDay, b.CategoryLabel, b.Note, "Approved", false, b.Color);
                Entries.Add(dto);
                return Ok(dto);
            }
            if (path.StartsWith("/api/entries/") && req.Method == HttpMethod.Put)
            {
                var id = path["/api/entries/".Length..];
                var b = (await req.Content!.ReadFromJsonAsync<UpdateEntryBody>(Json, ct))!;
                var i = Entries.FindIndex(e => e.Id == id);
                Entries[i] = Entries[i] with { Date = b.Date, EndDate = b.EndDate, StartTime = b.StartTime, EndTime = b.EndTime };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.StartsWith("/api/entries/") && req.Method == HttpMethod.Delete)
            {
                Entries.RemoveAll(e => e.Id == path["/api/entries/".Length..]);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };
    }

    private static (ApiStorageService Storage, FakeEntriesApi Api) Setup()
    {
        var fake = new FakeEntriesApi();
        var api = new ApiClient("http://fake.local", fake);
        return (new ApiStorageService(api, new BrowserSettingsStorage(new InMemoryBrowserKeyValueStore())), fake);
    }

    /// <summary>Wie CalendarViewModel einen Zeitraum speichert: Tag für Tag laden, ergänzen, speichern.</summary>
    private static async Task SaveSpanAsync(IStorageService storage, CalendarEntry template, DateOnly from, DateOnly to)
    {
        foreach (var (d, entry) in EntrySpans.Build(template, from, to, Guid.NewGuid().ToString()))
        {
            var day = await storage.LoadDayAsync(d);
            day.Entries.Add(entry);
            await storage.SaveDayAsync(day);
        }
    }

    private static CalendarEntry Messe(TimeSpan end) => new()
    {
        UserId = "u1", Type = EntryType.Work, Title = "Messe", AllDay = false,
        SpanStartTime = TimeSpan.FromHours(14), SpanEndTime = end
    };

    [Fact]
    public async Task Span_becomes_one_server_entry_with_its_corner_times()
    {
        var (storage, api) = Setup();

        await SaveSpanAsync(storage, Messe(TimeSpan.FromHours(10)), Wed, Wed.AddDays(2));

        var only = Assert.Single(api.Entries);
        Assert.Equal((Wed, Wed.AddDays(2)), (only.Date, only.EndDate!.Value));
        Assert.Equal((new TimeOnly(14, 0), new TimeOnly(10, 0)), (only.StartTime!.Value, only.EndTime!.Value));
    }

    [Fact]
    public async Task Saving_a_middle_day_again_keeps_the_span()
    {
        var (storage, api) = Setup();
        await SaveSpanAsync(storage, Messe(TimeSpan.FromHours(10)), Wed, Wed.AddDays(2));

        var thu = await storage.LoadDayAsync(Wed.AddDays(1));
        thu.Entries.Add(new CalendarEntry { UserId = "u2", Type = EntryType.Work, Title = "Remise",
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await storage.SaveDayAsync(thu);

        Assert.Equal(2, api.Entries.Count);
        Assert.Single(api.Entries, e => e.EndDate == Wed.AddDays(2));
    }

    [Fact]
    public async Task Saving_the_uncovered_last_day_does_not_delete_a_span_ending_at_midnight()
    {
        // Mi 14:00 → Fr 00:00: der Freitag hat keinen Anteil. Ohne Filter stand der Zeitraum beim
        // Speichern des Freitags ohne Gegenstück da und wurde gelöscht — samt Mittwoch und Donnerstag.
        var (storage, api) = Setup();
        await SaveSpanAsync(storage, Messe(TimeSpan.Zero), Wed, Wed.AddDays(2));

        var fri = await storage.LoadDayAsync(Wed.AddDays(2));
        Assert.Empty(fri.Entries);
        fri.Entries.Add(new CalendarEntry { UserId = "u2", Type = EntryType.Work, Title = "Remise",
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await storage.SaveDayAsync(fri);

        Assert.Single(api.Entries, e => e.EndDate == Wed.AddDays(2) && e.CategoryLabel == "Messe");
    }

    [Fact]
    public async Task All_day_free_day_goes_to_the_server_without_times()
    {
        var (storage, api) = Setup();
        var day = await storage.LoadDayAsync(Wed);
        day.Entries.Add(new CalendarEntry { UserId = "u1", Type = EntryType.Work, Title = "Frei", AllDay = true });
        await storage.SaveDayAsync(day);

        var e = Assert.Single(api.Entries);
        Assert.Null(e.StartTime);
        Assert.True((await storage.LoadDayAsync(Wed)).Entries.Single().IsAllDay);
    }
}
