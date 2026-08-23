using FlexFamilyCalendar.Api.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace FlexFamilyCalendar.Api.Tests;

// Projektvorgabe aus der CLAUDE.md: „Über die API alles loggen (Methode/Pfad/Status)".
// Die Kehrseite ist genauso wichtig: kein Body, keine Header — sonst steht das Passwort
// jedes Logins im Klartext im Log.
public class RequestLoggingMiddlewareTests
{
    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class LoggerSpy : ILogger<RequestLoggingMiddleware>
    {
        public List<Entry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex,
                                Func<TState, Exception?, string> formatter)
            => Entries.Add(new(level, formatter(state, ex), ex));
    }

    private static DefaultHttpContext Context(string method, string path, string query = "")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        if (query.Length > 0) ctx.Request.QueryString = new QueryString(query);
        return ctx;
    }

    private static async Task<LoggerSpy> RunAsync(HttpContext ctx, RequestDelegate next)
    {
        var spy = new LoggerSpy();
        await new RequestLoggingMiddleware(next, spy).InvokeAsync(ctx);
        return spy;
    }

    [Fact]
    public async Task Erfolgreicher_Aufruf_wird_mit_Methode_Pfad_und_Status_geloggt()
    {
        var ctx = Context("GET", "/api/users");
        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });

        var entry = Assert.Single(spy.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("GET", entry.Message);
        Assert.Contains("/api/users", entry.Message);
        Assert.Contains("200", entry.Message);
    }

    [Fact]
    public async Task Client_Fehler_wird_als_Warning_geloggt()
    {
        var ctx = Context("POST", "/api/auth/login");
        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 401; return Task.CompletedTask; });

        Assert.Equal(LogLevel.Warning, Assert.Single(spy.Entries).Level);
    }

    [Fact]
    public async Task Server_Fehler_wird_als_Error_geloggt()
    {
        var ctx = Context("GET", "/api/entries");
        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 503; return Task.CompletedTask; });

        Assert.Equal(LogLevel.Error, Assert.Single(spy.Entries).Level);
    }

    [Fact]
    public async Task Exception_wird_geloggt_und_weitergereicht()
    {
        var ctx = Context("PUT", "/api/settings");
        var spy = new LoggerSpy();
        var mw = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("kaputt"), spy);

        // Die Exception muss weiterfliegen — der ExceptionHandler weiter außen macht daraus
        // ProblemDetails. Die Middleware protokolliert nur, sie schluckt nicht.
        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(ctx));

        var entry = Assert.Single(spy.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task Authentifizierter_Aufruf_nennt_die_Benutzer_ID()
    {
        var ctx = Context("GET", "/api/entries");
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-42")], "test"));

        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
        Assert.Contains("user-42", Assert.Single(spy.Entries).Message);
    }

    [Fact]
    public async Task Anonymer_Aufruf_wird_als_anonym_geloggt()
    {
        var ctx = Context("GET", "/health");
        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });

        Assert.Contains("anonym", Assert.Single(spy.Entries).Message);
    }

    [Fact]
    public async Task Request_Body_landet_nicht_im_Log()
    {
        var ctx = Context("POST", "/api/auth/login");
        ctx.Request.Body = new MemoryStream("{\"username\":\"lars\",\"password\":\"geheim123\"}"u8.ToArray());

        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
        Assert.DoesNotContain("geheim123", Assert.Single(spy.Entries).Message);
    }

    [Fact]
    public async Task Authorization_Header_landet_nicht_im_Log()
    {
        var ctx = Context("GET", "/api/users");
        ctx.Request.Headers.Authorization = "Bearer eyJhbGciOiJIUzI1NiJ9.geheim";

        var spy = await RunAsync(ctx, c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
        Assert.DoesNotContain("geheim", Assert.Single(spy.Entries).Message);
    }
}
