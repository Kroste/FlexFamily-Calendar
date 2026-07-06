namespace FlexFamilyCalendar.Api.Settings;

/// <summary>
/// Reine Validierungs-/Normalisierungs-Regeln für <see cref="ServerSettingsDto"/>. Aus dem
/// Endpunkt herausgezogen, damit sie ohne WebApplicationFactory testbar sind — der Endpunkt
/// selbst wird zusätzlich per Integration-Test abgedeckt.
/// </summary>
public static class ServerSettingsRules
{
    public static string? Validate(ServerSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HolidayState))
            return "HolidayState darf nicht leer sein.";
        if (dto.OvernightHoursPerDay is < 0 or > 24)
            return "OvernightHoursPerDay muss zwischen 0 und 24 liegen.";
        return null;
    }

    /// <summary>Bundesland-Code auf ISO-Kürzel-Form bringen (BY, NW, …) — trim + upper.</summary>
    public static string NormalizeState(string state)
        => state.Trim().ToUpperInvariant();
}
