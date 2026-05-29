using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>HTTP-Client für die FlexFamilyCalendar-Server-API. Hält das JWT nach dem Login.</summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public string? Token { get; private set; }
    public ServerUserDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    /// <summary>Meldet an und merkt das Token für alle weiteren Aufrufe. Null = Anmeldung fehlgeschlagen.</summary>
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", new { username, password });
        if (!resp.IsSuccessStatusCode) return null;

        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is null || string.IsNullOrEmpty(login.Token)) return null;

        Token = login.Token;
        CurrentUser = login.User;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return login;
    }

    public async Task<List<ServerUserDto>> GetUsersAsync()
        => await _http.GetFromJsonAsync<List<ServerUserDto>>("api/users") ?? new();

    public async Task<List<ServerEntryDto>> GetEntriesAsync(DateOnly from, DateOnly to)
        => await _http.GetFromJsonAsync<List<ServerEntryDto>>(
               $"api/entries?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}") ?? new();

    public async Task<ServerEntryDto?> CreateEntryAsync(CreateEntryBody body)
    {
        var resp = await _http.PostAsJsonAsync("api/entries", body);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ServerEntryDto>() : null;
    }

    public async Task<bool> UpdateEntryAsync(string id, UpdateEntryBody body)
        => (await _http.PutAsJsonAsync($"api/entries/{id}", body)).IsSuccessStatusCode;

    public async Task<bool> DeleteEntryAsync(string id)
        => (await _http.DeleteAsync($"api/entries/{id}")).IsSuccessStatusCode;
}
