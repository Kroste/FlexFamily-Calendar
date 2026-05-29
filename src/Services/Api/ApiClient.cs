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
    public bool CurrentUserIsAdmin => string.Equals(CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>Setzt ein bereits vorhandenes Token (z.B. gemerkt) ohne erneute Anmeldung.</summary>
    public void SetToken(string token)
    {
        Token = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Prüft das aktuelle Token und liefert den zugehörigen Benutzer (null, wenn ungültig/abgelaufen).</summary>
    public async Task<ServerUserDto?> GetMeAsync()
    {
        var resp = await _http.GetAsync("api/auth/me");
        if (!resp.IsSuccessStatusCode) return null;
        var me = await resp.Content.ReadFromJsonAsync<ServerUserDto>();
        if (me is not null) CurrentUser = me;
        return me;
    }

    public async Task<ServerUserDto> UpdateMyProfileAsync(UpdateProfileBody body)
    {
        var resp = await _http.PutAsJsonAsync("api/auth/me", body);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Profil speichern");
        var dto = await resp.Content.ReadFromJsonAsync<ServerUserDto>();
        if (dto is not null) CurrentUser = dto;
        LogService.Info("API eigenes Profil aktualisiert");
        return dto!;
    }

    public async Task SetMyPasswordAsync(string password)
    {
        // Passwort nur im Body → wird durch den Logging-Handler nicht protokolliert.
        var resp = await _http.PostAsJsonAsync("api/auth/me/password", new { password });
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Kennwort setzen");
        LogService.Info("API eigenes Kennwort geändert");
    }

    public async Task<List<ServerUserDto>> GetUsersAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerUserDto>>("api/users") ?? new();
        LogService.Debug("API Benutzer geladen: {0}", list.Count);
        return list;
    }

    public async Task<ServerUserDto> CreateUserAsync(CreateUserBody body)
    {
        var resp = await _http.PostAsJsonAsync("api/users", body);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, $"Benutzer anlegen ({body.Username})");
        var dto = await resp.Content.ReadFromJsonAsync<ServerUserDto>();
        LogService.Info("API Benutzer angelegt: {0} (Rolle {1})", body.Username, body.Role);
        return dto!;
    }

    public async Task<ServerUserDto> UpdateUserAsync(string id, UpdateUserBody body)
    {
        var resp = await _http.PutAsJsonAsync($"api/users/{id}", body);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, $"Benutzer ändern ({body.Username})");
        var dto = await resp.Content.ReadFromJsonAsync<ServerUserDto>();
        LogService.Info("API Benutzer aktualisiert: {0}", body.Username);
        return dto!;
    }

    public async Task DeleteUserAsync(string id)
    {
        var resp = await _http.DeleteAsync($"api/users/{id}");
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Benutzer löschen");
        LogService.Info("API Benutzer gelöscht: id={0}", id);
    }

    public async Task SetUserPasswordAsync(string id, string password)
    {
        // Passwort steht nur im Body → wird durch den Logging-Handler nicht protokolliert.
        var resp = await _http.PostAsJsonAsync($"api/users/{id}/password", new { password });
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Kennwort setzen");
        LogService.Info("API Kennwort gesetzt: id={0}", id);
    }

    public async Task<List<ServerActivityTypeDto>> GetActivityTypesAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerActivityTypeDto>>("api/activity-types") ?? new();
        LogService.Debug("API Aktivitätstypen geladen: {0}", list.Count);
        return list;
    }

    public async Task ReplaceActivityTypesAsync(List<ServerActivityTypeDto> items)
    {
        var resp = await _http.PutAsJsonAsync("api/activity-types", items);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Aktivitätstypen speichern");
        LogService.Info("API Aktivitätstypen ersetzt: {0}", items.Count);
    }

    public async Task<List<ServerRecurringActivityDto>> GetRecurringActivitiesAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerRecurringActivityDto>>("api/recurring-activities") ?? new();
        LogService.Debug("API Wiederkehrende Aktivitäten geladen: {0}", list.Count);
        return list;
    }

    public async Task ReplaceRecurringActivitiesAsync(List<ServerRecurringActivityDto> items)
    {
        var resp = await _http.PutAsJsonAsync("api/recurring-activities", items);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Wiederkehrende Aktivitäten speichern");
        LogService.Info("API Wiederkehrende Aktivitäten ersetzt: {0}", items.Count);
    }

    public async Task<List<ServerSwapRequestDto>> GetSwapRequestsAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerSwapRequestDto>>("api/swap-requests") ?? new();
        LogService.Debug("API Schichttausch-Anfragen geladen: {0}", list.Count);
        return list;
    }

    public async Task ReplaceSwapRequestsAsync(List<ServerSwapRequestDto> items)
    {
        var resp = await _http.PutAsJsonAsync("api/swap-requests", items);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Schichttausch-Anfragen speichern");
        LogService.Info("API Schichttausch-Anfragen ersetzt: {0}", items.Count);
    }

    public async Task<List<ServerNotificationDto>> GetNotificationsAsync()
    {
        var list = await _http.GetFromJsonAsync<List<ServerNotificationDto>>("api/notifications") ?? new();
        LogService.Debug("API Benachrichtigungen geladen: {0}", list.Count);
        return list;
    }

    public async Task ReplaceNotificationsAsync(List<ServerNotificationDto> items)
    {
        var resp = await _http.PutAsJsonAsync("api/notifications", items);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Benachrichtigungen speichern");
        LogService.Info("API Benachrichtigungen ersetzt: {0}", items.Count);
    }

    public async Task<ServerDayNoteDto> GetDayNoteAsync(DateOnly date)
        => await _http.GetFromJsonAsync<ServerDayNoteDto>($"api/day-notes/{date:yyyy-MM-dd}")
           ?? new ServerDayNoteDto("", false);

    public async Task SetDayNoteAsync(DateOnly date, ServerDayNoteDto note)
    {
        var resp = await _http.PutAsJsonAsync($"api/day-notes/{date:yyyy-MM-dd}", note);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Tagesnotiz speichern");
        LogService.Debug("API Tagesnotiz gesetzt: {0:yyyy-MM-dd}", date);
    }

    /// <summary>Baut aus einer Fehlerantwort eine ApiException mit der Server-Meldung (falls vorhanden).</summary>
    private static async Task<ApiException> ErrorAsync(HttpResponseMessage resp, string what)
    {
        string? msg = null;
        try { msg = (await resp.Content.ReadFromJsonAsync<ApiErrorBody>())?.Error; } catch { /* kein JSON-Body */ }
        return new ApiException(string.IsNullOrWhiteSpace(msg)
            ? $"{what} fehlgeschlagen ({(int)resp.StatusCode})."
            : msg!);
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

    public async Task<SendWeekPlanResponseDto> SendWeekPlanAsync(SendWeekPlanBody body)
    {
        var resp = await _http.PostAsJsonAsync("api/mail/send-week-plan", body);
        if (!resp.IsSuccessStatusCode) throw await ErrorAsync(resp, "Mail-Versand");
        var dto = await resp.Content.ReadFromJsonAsync<SendWeekPlanResponseDto>();
        LogService.Info("API Mail-Versand: gesendet={0} fehlgeschlagen={1}", dto?.Sent ?? 0, dto?.Failed ?? 0);
        return dto ?? new SendWeekPlanResponseDto(0, 0, new List<string>());
    }
}
