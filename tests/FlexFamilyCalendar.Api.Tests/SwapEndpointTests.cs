using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Models;
using FlexFamilyCalendar.Api.Swaps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FlexFamilyCalendar.Api.Data;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>
/// Schichttausch über die echten Endpunkte. Der Kern: nach dem Annehmen gehört die Schicht dem
/// anderen — vorher stand der Tausch auf „angenommen", die Schicht wechselte aber nie den Besitzer.
/// Jeder Test legt eigene Nutzer und Tage an, die Fixture teilt die DB über die ganze Klasse.
/// </summary>
public class SwapEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public SwapEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _dayCounter;

    /// <summary>Jeder Aufruf ein eigener Tag, damit sich Schichten verschiedener Tests nie überschneiden.</summary>
    private static DateOnly FreshDay() => new DateOnly(2028, 1, 3).AddDays(Interlocked.Increment(ref _dayCounter) * 3);

    private async Task<(Guid Id, HttpClient Client)> NewUserAsync(string role = "User")
    {
        var name = "s-" + Guid.NewGuid().ToString("N")[..10];
        var id = Guid.NewGuid();
        _factory.Seed(db => db.Users.Add(new UserEntity
        {
            Id = id, Username = name, DisplayName = name, Role = role, Category = "Employee",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw")
        }));
        return (id, await _factory.CreateAuthenticatedClientAsync(name, "pw"));
    }

    private Guid SeedShift(Guid owner, DateOnly date, int from, int to)
    {
        var id = Guid.NewGuid();
        _factory.Seed(db => db.Entries.Add(new CalendarEntry
        {
            Id = id, UserId = owner, Type = EntryTypes.Work, Date = date,
            StartTime = new TimeOnly(from, 0), EndTime = new TimeOnly(to, 0),
            Status = EntryStatus.Approved, CreatedBy = owner
        }));
        return id;
    }

    private Guid OwnerOf(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Entries.AsNoTracking().Single(e => e.Id == entryId).UserId;
    }

    private static async Task<ShiftSwapRequestDto> OfferAsync(HttpClient c, CreateSwapRequest req)
    {
        var resp = await c.PostAsJsonAsync("api/swap-requests", req, Ct);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(Ct))!;
    }

    [Fact]
    public async Task Accepting_a_giveaway_moves_the_shift()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var shift = SeedShift(anna, FreshDay(), 8, 16);

        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, "Arzttermin"));
        var resp = await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(bert, OwnerOf(shift));
        var after = await resp.Content.ReadFromJsonAsync<ShiftSwapRequestDto>(Ct);
        Assert.Equal(SwapRules.Accepted, after!.Status);
    }

    [Fact]
    public async Task Accepting_an_exchange_moves_both_shifts()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var annas = SeedShift(anna, FreshDay(), 8, 12);
        var berts = SeedShift(bert, FreshDay(), 14, 18);

        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.Exchange, null, annas.ToString(), bert.ToString(), berts.ToString(), null));
        var resp = await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(bert, OwnerOf(annas));
        Assert.Equal(anna, OwnerOf(berts));
    }

    [Fact]
    public async Task Offerer_cannot_accept_their_own_offer()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, _) = await NewUserAsync();
        var shift = SeedShift(anna, FreshDay(), 8, 16);
        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, null));

        var resp = await annaClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal(anna, OwnerOf(shift));
    }

    [Fact]
    public async Task Multi_day_assignment_cannot_be_offered()
    {
        // Ein mehrtägiger Einsatz ist EIN Eintrag — tageweise tauschen gibt das Modell nicht her.
        var (anna, annaClient) = await NewUserAsync();
        var (bert, _) = await NewUserAsync();
        var day = FreshDay();
        var id = Guid.NewGuid();
        _factory.Seed(db => db.Entries.Add(new CalendarEntry
        {
            Id = id, UserId = anna, Type = EntryTypes.Work, Date = day, EndDate = day.AddDays(2),
            StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(10, 0),
            Status = EntryStatus.Approved, CreatedBy = anna
        }));

        var resp = await annaClient.PostAsJsonAsync("api/swap-requests",
            new CreateSwapRequest(SwapRules.GiveAway, anna.ToString(), id.ToString(), bert.ToString(), null, null), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_offer_someone_elses_shift()
    {
        var (anna, _) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var annas = SeedShift(anna, FreshDay(), 8, 16);

        // Bert bietet Annas Schicht sich selbst an — und nimmt sie dann an: so hätte man sich
        // jede fremde Schicht holen können.
        var resp = await bertClient.PostAsJsonAsync("api/swap-requests",
            new CreateSwapRequest(SwapRules.GiveAway, anna.ToString(), annas.ToString(), bert.ToString(), null, null), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_may_propose_on_behalf_of_someone()
    {
        // Der KI-Planer schlägt im Namen der Mitarbeiter vor.
        var (anna, _) = await NewUserAsync();
        var (bert, _) = await NewUserAsync();
        var (_, admin) = await NewUserAsync("Admin");
        var shift = SeedShift(anna, FreshDay(), 8, 16);

        var swap = await OfferAsync(admin, new CreateSwapRequest(SwapRules.GiveAway, anna.ToString(), shift.ToString(), bert.ToString(), null, null));

        Assert.Equal(anna.ToString(), swap.FromUserId);
    }

    [Fact]
    public async Task Server_takes_names_and_dates_from_the_database()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, _) = await NewUserAsync();
        var day = FreshDay();
        var shift = SeedShift(anna, day, 8, 16);

        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, null));

        Assert.Equal(day.ToString("yyyy-MM-dd"), swap.FromDate);
        Assert.False(string.IsNullOrEmpty(swap.FromUserName));
        Assert.False(string.IsNullOrEmpty(swap.ToUserName));
    }

    [Fact]
    public async Task Outsiders_do_not_see_the_swap()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var (_, carlClient) = await NewUserAsync();
        var shift = SeedShift(anna, FreshDay(), 8, 16);
        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, "privat"));

        var forCarl = await carlClient.GetFromJsonAsync<List<ShiftSwapRequestDto>>("api/swap-requests", Ct);
        var forBert = await bertClient.GetFromJsonAsync<List<ShiftSwapRequestDto>>("api/swap-requests", Ct);

        Assert.DoesNotContain(forCarl!, s => s.Id == swap.Id);
        Assert.Contains(forBert!, s => s.Id == swap.Id);
        // Außenstehende können auch nicht darauf antworten — für sie existiert er nicht.
        Assert.Equal(HttpStatusCode.NotFound,
            (await carlClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Accepting_twice_is_refused()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var shift = SeedShift(anna, FreshDay(), 8, 16);
        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, null));

        await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);
        var second = await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Accept_refuses_an_overlap()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var day = FreshDay();
        var annas = SeedShift(anna, day, 8, 16);
        SeedShift(bert, day, 12, 20);          // Bert arbeitet an dem Tag schon überlappend

        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, annas.ToString(), bert.ToString(), null, null));
        var resp = await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains(SwapRules.ErrorOverlap, await resp.Content.ReadAsStringAsync(Ct));
        Assert.Equal(anna, OwnerOf(annas));
    }

    [Fact]
    public async Task Accept_refuses_a_finalized_day()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var day = FreshDay();
        var shift = SeedShift(anna, day, 8, 16);
        var swap = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, shift.ToString(), bert.ToString(), null, null));
        _factory.Seed(db => db.DayMeta.Add(new CalendarDayMeta { Date = day, Note = "", IsFinalized = true }));

        var resp = await bertClient.PostAsync($"api/swap-requests/{swap.Id}/accept", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains(SwapRules.ErrorFinalized, await resp.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Reject_and_withdraw_respect_the_roles()
    {
        var (anna, annaClient) = await NewUserAsync();
        var (bert, bertClient) = await NewUserAsync();
        var s1 = SeedShift(anna, FreshDay(), 8, 16);
        var s2 = SeedShift(anna, FreshDay(), 8, 16);
        var a = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, s1.ToString(), bert.ToString(), null, null));
        var b = await OfferAsync(annaClient, new CreateSwapRequest(SwapRules.GiveAway, null, s2.ToString(), bert.ToString(), null, null));

        // Anna kann nicht für Bert ablehnen, Bert nicht für Anna zurückziehen.
        Assert.Equal(HttpStatusCode.Forbidden, (await annaClient.PostAsync($"api/swap-requests/{a.Id}/reject", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bertClient.PostAsync($"api/swap-requests/{b.Id}/withdraw", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await bertClient.PostAsync($"api/swap-requests/{a.Id}/reject", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await annaClient.PostAsync($"api/swap-requests/{b.Id}/withdraw", null, Ct)).StatusCode);
        Assert.Equal(anna, OwnerOf(s1));
        Assert.Equal(anna, OwnerOf(s2));
    }

    [Fact]
    public async Task Replace_all_is_gone()
    {
        var (_, client) = await NewUserAsync();

        var resp = await client.PutAsJsonAsync("api/swap-requests", Array.Empty<object>(), Ct);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }
}
