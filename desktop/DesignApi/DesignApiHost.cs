using System.Security.Cryptography;
using System.Text;
using FlexFamilyCalendar.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlexFamilyCalendar.DesignApi;

/// <summary>
/// Lokale REST-Schnittstelle zur Design- und UI-Prüfung (Kroste-Muster).
///
/// <para><b>Warum das und nicht Fernsteuerung von außen:</b> <c>SetForegroundWindow</c> plus
/// <c>mouse_event</c> plus <c>PrintWindow</c> sieht für einen verhaltensbasierten Virenscanner
/// aus wie ein RAT und wird blockiert. Dazu verweigert Windows den Fokus gern, DPI-Skalierung
/// verschiebt Klick-Koordinaten, und verdeckte Fenster liefern das falsche Bild. Hier passiert
/// alles im eigenen Prozess: der Screenshot kommt aus <c>RenderTargetBitmap</c>, der Klick aus
/// <c>ICommand.Execute</c>.</para>
///
/// <para><b>Standardmäßig aus.</b> Nur aktiv mit <c>--api-port</c>, nur auf Loopback, und ohne
/// gesetztes Token antwortet jede Anfrage mit 403 — nicht etwa „offen, weil nichts
/// konfiguriert". Schreibende Aktionen sind zusätzlich gesperrt, siehe
/// <see cref="DestructiveGuard"/>.</para>
///
/// <code>
/// FlexFamilyCalendar.Desktop --api-port 8765 --api-token geheim --auto-shutdown-after 10m
/// curl -s -H "Authorization: Bearer geheim" http://127.0.0.1:8765/state
/// curl -s -H "Authorization: Bearer geheim" -X POST http://127.0.0.1:8765/screenshot -o shot.png
/// </code>
/// </summary>
public static class DesignApiHost
{
    private static WebApplication? _app;

    /// <summary>
    /// Startet die API, wenn <c>--api-port</c> übergeben wurde; sonst passiert nichts.
    /// Ein Fehlschlag (Port belegt) ist nicht fatal — eine Prüf-Nebenfunktion darf die App
    /// niemals am Starten hindern.
    /// </summary>
    public static void MaybeStart(string[] args)
    {
        if (DesignApiOptions.Parse(args) is not { } options) return;

        try
        {
            var actions = new UiActions(options.AllowClicks);

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(options.Port));

            var app = builder.Build();
            _app = app;

            app.Use(async (ctx, next) =>
            {
                if (!IsAuthorized(ctx, options.Token))
                {
                    await Problem(ctx, StatusCodes.Status403Forbidden, "Kein oder falsches Bearer-Token.");
                    return;
                }
                await next();
            });

            MapEndpoints(app, actions, options);

            _ = app.RunAsync($"http://127.0.0.1:{options.Port}");

            LogService.Info("Design-Test-API auf http://127.0.0.1:{0} (Loopback, Bearer{1}).",
                options.Port, options.AllowClicks ? ", Klicks frei" : ", nur lesend");

            if (options.AutoShutdownAfter is { } after) StartAutoShutdown(after);
        }
        catch (Exception ex)
        {
            LogService.Warn("Design-Test-API konnte nicht starten ({0}) — App läuft ohne sie weiter.",
                ex.Message);
        }
    }

    private static void StartAutoShutdown(TimeSpan after) => _ = Task.Run(async () =>
    {
        await Task.Delay(after);
        LogService.Info("Auto-Shutdown der Design-API nach {0:0.#} min.", after.TotalMinutes);
        try { await (_app?.StopAsync() ?? Task.CompletedTask); }
        catch (Exception ex) { LogService.Debug("Stop der Design-API: {0}", ex.Message); }
        Environment.Exit(0);
    });

    private static void MapEndpoints(WebApplication app, UiActions a, DesignApiOptions options)
    {
        app.MapGet("/state", async () => Results.Json(await a.GetStateAsync()));

        app.MapGet("/elements", async (HttpContext ctx) =>
            Results.Json(new { elements = await a.ListElementsAsync(ctx.Request.Query["window"]) }));

        app.MapPost("/screenshot", async (HttpContext ctx) =>
        {
            var png = await a.ScreenshotAsync(ctx.Request.Query["target"]);
            if (png.Length == 0)
                return Results.Problem("Kein Fenster zum Abbilden.", statusCode: StatusCodes.Status404NotFound);

            return ctx.Request.Query["format"] == "json"
                ? Results.Json(new { pngBase64 = Convert.ToBase64String(png) })
                : Results.File(png, "image/png");
        });

        app.MapPost("/theme", async (HttpContext ctx) =>
        {
            var variant = ctx.Request.Query["variant"].ToString();
            string[] known = ["Light", "Dark", "System"];
            if (!known.Contains(variant, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "unbekannte Variante", available = known },
                    statusCode: StatusCodes.Status404NotFound);

            await a.SetThemeAsync(variant);
            return Results.Ok(new { theme = variant });
        });

        app.MapPost("/language", async (HttpContext ctx) =>
        {
            var code = ctx.Request.Query["code"].ToString();
            string[] known = ["de", "en"];
            if (!known.Contains(code, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "unbekannte Sprache", available = known },
                    statusCode: StatusCodes.Status404NotFound);

            await a.SetLanguageAsync(code.ToLowerInvariant());
            return Results.Ok(new { language = code.ToLowerInvariant() });
        });

        app.MapPost("/open", async (HttpContext ctx) =>
        {
            var window = ctx.Request.Query["window"].ToString();
            return await a.OpenWindowAsync(window)
                ? Results.Ok(new { opened = window })
                : Results.Json(new { error = "unbekanntes Fenster", available = UiActions.OpenableWindows },
                    statusCode: StatusCodes.Status404NotFound);
        });

        app.MapPost("/close", async (HttpContext ctx) =>
        {
            var window = ctx.Request.Query["window"].ToString();
            if (string.IsNullOrEmpty(window)) window = "topmost";

            return await a.CloseWindowAsync(window)
                ? Results.Ok(new { closed = window })
                : Results.Problem("Nichts zu schließen (das Hauptfenster bleibt offen).",
                    statusCode: StatusCodes.Status404NotFound);
        });

        app.MapPost("/click", async (HttpContext ctx) =>
        {
            var element = ctx.Request.Query["element"].ToString();
            var result = await a.ClickAsync(element);

            return result switch
            {
                ClickResult.Ok => Results.Ok(new { clicked = element }),

                // 403 statt 409: eine Berechtigungsfrage, kein Zustand. Ein Retry hilft nicht.
                ClickResult.NotEnabled => Results.Json(
                    new { error = "Klicks sind aus. Mit --api-allow-clicks starten." },
                    statusCode: StatusCodes.Status403Forbidden),
                ClickResult.Blocked => Results.Json(
                    new { error = "gesperrt", element, reason = DestructiveGuard.ReasonFor(element) },
                    statusCode: StatusCodes.Status403Forbidden),

                ClickResult.NotFound => Results.Json(
                    new { error = "Element nicht gefunden", element,
                          hint = "Namen über GET /elements abfragen" },
                    statusCode: StatusCodes.Status404NotFound),
                _ => Results.Json(new { error = "nicht klickbar", element },
                    statusCode: StatusCodes.Status400BadRequest),
            };
        });
    }

    private static async Task Problem(HttpContext ctx, int status, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(new { status, detail });
    }

    private static bool IsAuthorized(HttpContext ctx, string? token)
    {
        // Kein Token konfiguriert heißt zu, nicht offen.
        if (string.IsNullOrEmpty(token)) return false;

        var header = ctx.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.Ordinal)) return false;

        // Konstante Laufzeit, damit der Vergleich das Token nicht zeichenweise verrät.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(header[prefix.Length..]),
            Encoding.UTF8.GetBytes(token));
    }
}
