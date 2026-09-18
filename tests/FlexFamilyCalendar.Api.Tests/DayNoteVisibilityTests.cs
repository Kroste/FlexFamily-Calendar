using System.Net.Http.Json;
using FlexFamilyCalendar.Api.DayNotes;
using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Tests;

/// <summary>Adressierte Tagesnotizen sieht nur, wen sie betreffen — und der Admin.</summary>
public class DayNoteVisibilityTests
{
    private static readonly Guid Mara = Guid.NewGuid();
    private static readonly Guid Tim = Guid.NewGuid();

    [Fact]
    public void Note_without_addressee_is_public()
    {
        var (note, to) = DayNoteVisibility.Project("Elternabend", null, Tim, isAdmin: false);
        Assert.Equal("Elternabend", note);
        Assert.Null(to);
    }

    [Fact]
    public void Addressee_sees_their_note()
    {
        var (note, to) = DayNoteVisibility.Project("Gespräch 14 Uhr", Mara.ToString(), Mara, isAdmin: false);
        Assert.Equal("Gespräch 14 Uhr", note);
        Assert.Equal(Mara.ToString(), to);
    }

    [Fact]
    public void Addressee_match_ignores_guid_casing()
    {
        var (note, _) = DayNoteVisibility.Project("x", Mara.ToString().ToUpperInvariant(), Mara, isAdmin: false);
        Assert.Equal("x", note);
    }

    [Fact]
    public void Admin_sees_every_note()
    {
        var (note, to) = DayNoteVisibility.Project("Gespräch 14 Uhr", Mara.ToString(), Tim, isAdmin: true);
        Assert.Equal("Gespräch 14 Uhr", note);
        Assert.Equal(Mara.ToString(), to);
    }

    [Fact]
    public void Third_party_sees_neither_text_nor_addressee()
    {
        var (note, to) = DayNoteVisibility.Project("Gespräch 14 Uhr", Mara.ToString(), Tim, isAdmin: false);
        Assert.Equal("", note);
        Assert.Null(to);
    }
}

/// <summary>Dieselbe Regel über die echten Endpunkte — Einzeltag und Wochenbereich.</summary>
public class DayNoteEndpointVisibilityTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public DayNoteEndpointVisibilityTests(ApiTestFactory factory) => _factory = factory;

    // Eigener Zeitraum je Test, siehe CLAUDE.md zur geteilten Fixture-DB.
    private DateOnly SeedNoteForAdmin(DateOnly date)
    {
        _factory.Seed(db =>
        {
            var admin = db.Users.Single(u => u.Username == ApiTestFactory.AdminUser);
            db.DayMeta.Add(new CalendarDayMeta
            {
                Date = date, Note = "Personalgespräch", NoteUserId = admin.Id.ToString(), IsFinalized = true
            });
        });
        return date;
    }

    [Fact]
    public async Task Single_day_hides_note_addressed_to_someone_else()
    {
        var date = SeedNoteForAdmin(new DateOnly(2027, 1, 4));
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var dto = await client.GetFromJsonAsync<DayNoteDto>($"api/day-notes/{date:yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal("", dto!.Note);
        Assert.Null(dto.NoteUserId);
        Assert.True(dto.IsFinalized);      // die Freigabe selbst ist keine Privatsache
    }

    [Fact]
    public async Task Week_range_hides_note_addressed_to_someone_else()
    {
        var date = SeedNoteForAdmin(new DateOnly(2027, 2, 1));
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);

        var notes = await client.GetFromJsonAsync<List<DayNoteRangeDto>>(
            $"api/day-notes?from={date:yyyy-MM-dd}&to={date.AddDays(6):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        var dto = Assert.Single(notes!, n => n.Date == date);
        Assert.Equal("", dto.Note);
        Assert.Null(dto.NoteUserId);
    }

    [Fact]
    public async Task Addressee_still_gets_their_note_over_the_api()
    {
        var date = SeedNoteForAdmin(new DateOnly(2027, 3, 1));
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        var dto = await client.GetFromJsonAsync<DayNoteDto>($"api/day-notes/{date:yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        Assert.Equal("Personalgespräch", dto!.Note);
    }
}
