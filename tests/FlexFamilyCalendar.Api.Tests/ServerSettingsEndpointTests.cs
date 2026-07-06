using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Settings;

namespace FlexFamilyCalendar.Api.Tests;

// Kernvertrag /api/settings (Schritt 4): eingeloggte User lesen, nur Admin schreibt. Validierung
// blockt leere/negative Werte; der Bundesland-Code wird normalisiert (upper).
public class ServerSettingsEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public ServerSettingsEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_ohne_Auth_gibt_401()
    {
        var client = _factory.CreateSeededClient();
        var resp = await client.GetAsync("api/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GET_mit_User_liefert_Defaults()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);
        var dto = await client.GetFromJsonAsync<ServerSettingsDto>("api/settings");
        Assert.NotNull(dto);
        Assert.Equal("BY", dto!.HolidayState);
        Assert.Equal(2.0, dto.OvernightHoursPerDay);
    }

    [Fact]
    public async Task PUT_mit_User_ist_verboten()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);
        var resp = await client.PutAsJsonAsync("api/settings", new ServerSettingsDto("NW", 3.0));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PUT_mit_Admin_setzt_und_normalisiert_Bundesland()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);
        var resp = await client.PutAsJsonAsync("api/settings", new ServerSettingsDto("nw", 3.5));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<ServerSettingsDto>();
        Assert.Equal("NW", dto!.HolidayState);
        Assert.Equal(3.5, dto.OvernightHoursPerDay);

        // Wert bleibt persistent für die nächste Anfrage.
        var fresh = await client.GetFromJsonAsync<ServerSettingsDto>("api/settings");
        Assert.Equal("NW", fresh!.HolidayState);
        Assert.Equal(3.5, fresh.OvernightHoursPerDay);
    }

    [Fact]
    public async Task PUT_mit_leerem_Bundesland_gibt_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);
        var resp = await client.PutAsJsonAsync("api/settings", new ServerSettingsDto("   ", 2.0));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PUT_mit_negativen_Stunden_gibt_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);
        var resp = await client.PutAsJsonAsync("api/settings", new ServerSettingsDto("BY", -1));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
