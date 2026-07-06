using NLog;
using System.Text.RegularExpressions;

namespace FlexFamilyCalendar.Services;

public static partial class LogService
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    /// <summary>Fired for every Info/Warn/Error message so the status bar can display it.</summary>
    public static event Action<string>? StatusUpdated;

    public static void Info(string message, params object?[] args)
    {
        var msg = Format(message, args);
        _log.Info(msg);
        StatusUpdated?.Invoke(msg);
    }

    public static void Debug(string message, params object?[] args)
        => _log.Debug(Format(message, args));

    public static void Warn(string message, params object?[] args)
    {
        var msg = Format(message, args);
        _log.Warn(msg);
        StatusUpdated?.Invoke($"⚠ {msg}");
    }

    public static void Error(string message, Exception? ex = null)
    {
        var msg = Sanitize(message);
        if (ex != null)
            _log.Error(ex, msg);
        else
            _log.Error(msg);
        StatusUpdated?.Invoke($"Fehler: {msg}");
    }

    public static void Fatal(string message, Exception? ex = null)
    {
        var msg = Sanitize(message);
        if (ex != null)
            _log.Fatal(ex, msg);
        else
            _log.Fatal(msg);
        StatusUpdated?.Invoke($"FATAL: {msg}");
    }

    public static void UserAction(string username, string action)
    {
        var sanitized = Sanitize(action);
        _log.Info("[AKTION] Benutzer={0} | {1}", username, sanitized);
        StatusUpdated?.Invoke(sanitized);
    }

    public static void Click(string username, string element)
        => _log.Debug("[KLICK] Benutzer={0} | Element={1}", username, element);

    private static string Format(string template, object?[] args)
        => Sanitize(args.Length > 0 ? string.Format(template, args) : template);

    [GeneratedRegex(
        @"(?i)(api[_\-]?key|apikey|access_token|refresh_token|client_secret|secret|password|passwd|bearer|authorization)\s*[=:""\s]\s*\S+",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitivePattern();

    private static string Sanitize(string message)
        => SensitivePattern().Replace(message, "$1=[REDACTED]");
}
