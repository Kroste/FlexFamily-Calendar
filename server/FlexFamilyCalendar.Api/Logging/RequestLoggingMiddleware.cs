using System.Diagnostics;
using System.Security.Claims;

namespace FlexFamilyCalendar.Api.Logging;

/// <summary>
/// Loggt jeden HTTP-Aufruf mit Methode, Pfad, Status, Dauer und — falls authentifiziert —
/// der Benutzer-ID. Projektvorgabe aus der CLAUDE.md: „Über die API alles loggen".
///
/// Bewusst NICHT geloggt werden Request- und Response-Body sowie Header. Der Login-Endpunkt
/// bekäme sonst jedes Passwort ins Log, und Authorization-Header jedes JWT. Der Query-String
/// wird mitgenommen, aber durch den ${masked}-Renderer gedreht, falls doch mal ein Token
/// darin landet.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Der ExceptionHandler weiter außen macht daraus ProblemDetails; hier geht es nur
            // darum, dass der Aufruf nicht ohne Logzeile verschwindet.
            logger.LogError(ex, "{Method} {Path}{Query} → Exception nach {Elapsed} ms (Benutzer {User})",
                method, path, query, sw.ElapsedMilliseconds, UserOf(context));
            throw;
        }

        sw.Stop();
        var status = context.Response.StatusCode;
        var level = status >= 500 ? LogLevel.Error
                  : status >= 400 ? LogLevel.Warning
                  : LogLevel.Information;

        logger.Log(level, "{Method} {Path}{Query} → {Status} in {Elapsed} ms (Benutzer {User})",
            method, path, query, status, sw.ElapsedMilliseconds, UserOf(context));
    }

    private static string UserOf(HttpContext context)
        => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? context.User.FindFirstValue("sub")
           ?? "anonym";
}
