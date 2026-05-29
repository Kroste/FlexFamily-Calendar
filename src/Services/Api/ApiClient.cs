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
        var handler = new ApiLoggingHandler(new HttpClientHandler());
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    /// <summary>Meldet an und merkt das Token für alle weiteren Aufrufe. Null = Anmeldung fehlgeschlagen.</summary>
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        // Passwort wird NICHT geloggt (auch der Logging-Handler protokolliert keine Inhalte).
        var resp = await _http.PostAsJsonAsync("api/auth/login", new { username, password });
        if (!resp.IsSuccessStatusCode) return null;

        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is null || string.IsNullOrEmpty(login.Token)) return null;

        Token = login.Token;
        CurrentUser = login.User;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        LogService.Debug("API Login: Token erhalten für {0} (Rolle {1})", login.User.Username, login.User.Role);
        return login;
    }

    public async Task<List<ServerUserDto>> GetUsersAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerUserDto>>("api/users") ?? new();
        LogService.Debug("API Benutzer geladen: {0}", list.Count);
        return list;
    }

    public async Task<List<ServerEntryDto>> GetEntriesAsync(DateOnly from, DateOnly to)
    {
        var list = await _http.GetFromJsonAsync<List<ServerEntryDto>>(
            $"api/entries?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}") ?? new();
        LogService.Debug("API Einträge {0:yyyy-MM-dd}..{1:yyyy-MM-dd}: {2} geladen", from, to, list.Count);
        return list;
    }

    public async Task<ServerEntryDto?> CreateEntryAsync(CreateEntryBody body)
    {
        var resp = await _http.PostAsJsonAsync("api/entries", body);
        if (!resp.IsSuccessStatusCode)
        {
            LogService.Warn("API Eintrag anlegen fehlgeschlagen: Typ={0} Datum={1:yyyy-MM-dd} User={2} → {3}",
                body.Type, body.Date, body.UserId, (int)resp.StatusCode);
            return null;
        }
        var dto = await resp.Content.ReadFromJsonAsync<ServerEntryDto>();
        LogService.Info("API Eintrag angelegt: id={0} Typ={1} Datum={2:yyyy-MM-dd} User={3}",
            dto?.Id, body.Type, body.Date, body.UserId);
        return dto;
    }

    public async Task<bool> UpdateEntryAsync(string id, UpdateEntryBody body)
    {
        var ok = (await _http.PutAsJsonAsync($"api/entries/{id}", body)).IsSuccessStatusCode;
        LogService.Info("API Eintrag aktualisiert: id={0} → {1}", id, ok ? "ok" : "fehlgeschlagen");
        return ok;
    }

    public async Task<bool> DeleteEntryAsync(string id)
    {
        var ok = (await _http.DeleteAsync($"api/entries/{id}")).IsSuccessStatusCode;
        LogService.Info("API Eintrag gelöscht: id={0} → {1}", id, ok ? "ok" : "fehlgeschlagen");
        return ok;
    }
}
