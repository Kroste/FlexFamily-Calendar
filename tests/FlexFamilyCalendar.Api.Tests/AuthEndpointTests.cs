using System.Net;
using System.Net.Http.Json;

namespace FlexFamilyCalendar.Api.Tests;

public class AuthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public AuthEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_mit_falschem_Passwort_gibt_401()
    {
        var client = _factory.CreateSeededClient();
        var resp = await client.PostAsJsonAsync("api/auth/login",
            new { username = ApiTestFactory.AdminUser, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_mit_unbekanntem_Benutzer_gibt_401()
    {
        var client = _factory.CreateSeededClient();
        var resp = await client.PostAsJsonAsync("api/auth/login",
            new { username = "gibtsnicht", password = "xxx" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_erfolgreich_liefert_Token_und_User()
    {
        var client = _factory.CreateSeededClient();
        var resp = await client.PostAsJsonAsync("api/auth/login",
            new { username = ApiTestFactory.AdminUser, password = ApiTestFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"token\"", body);
        Assert.Contains("\"user\"", body);
    }
}
