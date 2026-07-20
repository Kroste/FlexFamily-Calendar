using System.Net;
using System.Net.Http.Json;
using FlexFamilyCalendar.Api.Users;

namespace FlexFamilyCalendar.Api.Tests;

public class UserReorderEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    public UserReorderEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task NichtAdmin_kann_Reihenfolge_nicht_setzen()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.PlainUser, ApiTestFactory.PlainPassword);
        var resp = await client.PostAsJsonAsync("api/users/order",
            new { userIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_setzt_Reihenfolge_und_GET_users_liefert_neue_Reihenfolge()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(
            ApiTestFactory.AdminUser, ApiTestFactory.AdminPassword);

        var before = await client.GetFromJsonAsync<List<UserDto>>("api/users");
        Assert.NotNull(before);
        Assert.True(before!.Count >= 2);

        // Reihenfolge umdrehen und speichern.
        var reversed = before.AsEnumerable().Reverse().Select(u => u.Id).ToArray();
        var putResp = await client.PostAsJsonAsync("api/users/order",
            new { userIds = reversed });
        Assert.Equal(HttpStatusCode.NoContent, putResp.StatusCode);

        var after = await client.GetFromJsonAsync<List<UserDto>>("api/users");
        Assert.NotNull(after);
        Assert.Equal(reversed, after!.Select(u => u.Id).ToArray());
        // Erster hat den kleinsten PlanOrder-Index bekommen.
        Assert.Equal(0, after[0].PlanOrder);
    }
}
