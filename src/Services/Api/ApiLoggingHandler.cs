using System.Diagnostics;
using System.Net.Http;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>
/// Loggt jeden API-Aufruf: Methode, Pfad, Statuscode, Dauer. Bewusst werden WEDER
/// Request-/Response-Inhalte NOCH Header geloggt → keine Passwörter, kein Token.
/// </summary>
public class ApiLoggingHandler : DelegatingHandler
{
    public ApiLoggingHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? request.RequestUri?.ToString() ?? "?";
        var sw = Stopwatch.StartNew();
        try
        {
            var resp = await base.SendAsync(request, cancellationToken);
            sw.Stop();
            if (resp.IsSuccessStatusCode)
                LogService.Debug("API {0} {1} → {2} ({3} ms)", request.Method, path, (int)resp.StatusCode, sw.ElapsedMilliseconds);
            else
                LogService.Warn("API {0} {1} → {2} ({3} ms)", request.Method, path, (int)resp.StatusCode, sw.ElapsedMilliseconds);
            return resp;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogService.Warn("API {0} {1} → FEHLER nach {2} ms: {3}", request.Method, path, sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
