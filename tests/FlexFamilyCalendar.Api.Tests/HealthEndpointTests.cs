using System.Net;

namespace FlexFamilyCalendar.Api.Tests;

// Health-Endpunkt ist bewusst ohne Auth — Docker/Caddy fragt ihn beim Boot ab. Wenn wir ihn
// versehentlich in die Auth-Kette hängen, sollte dieser Test rot werden.
public class HealthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public HealthEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_liefert_200_ohne_Auth()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"status\"", body);
    }
}
