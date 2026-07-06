namespace FlexFamilyCalendar.Api.Settings;

/// <summary>
/// Transfer-Objekt für <c>/api/settings</c> — spiegelt <see cref="Models.ServerSettingsEntity"/>
/// ohne die interne Id.
/// </summary>
public record ServerSettingsDto(string HolidayState, double OvernightHoursPerDay);
