namespace FlexFamilyCalendar.Services.Api;

/// <summary>
/// Fehler aus einem API-Aufruf, mit der Server-Fehlermeldung. Erbt von InvalidOperationException,
/// damit die UI Server- und lokale Validierungsfehler einheitlich behandeln kann.
/// </summary>
public class ApiException : InvalidOperationException
{
    public ApiException(string message) : base(message) { }
}
