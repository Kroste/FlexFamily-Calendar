using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Theming;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;

namespace FlexFamilyCalendar.DesignApi;

/// <summary>Ein offenes Fenster im Zustands-Abzug.</summary>
public sealed record WindowInfo(string Title, string Type, double Width, double Height, bool IsActive);

/// <summary>Antwort auf <c>GET /state</c>.</summary>
public sealed record StateSnapshot(
    string Language,
    string ThemeVariant,
    bool ClicksAllowed,
    IReadOnlyList<WindowInfo> Windows);

/// <summary>
/// Alle UI-Zugriffe der Design-Test-API. Jeder einzelne läuft über den
/// <see cref="Dispatcher.UIThread"/>, und die Abzüge werden **komplett innerhalb** des
/// Dispatcher-Lambdas gebaut: nur das ViewModel zu holen und danach im HTTP-Thread durch
/// seine Collections zu laufen wäre eine Race Condition.
/// </summary>
public sealed class UiActions(bool allowClicks)
{
    private static IClassicDesktopStyleApplicationLifetime? Desktop
        => Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    public Task<StateSnapshot> GetStateAsync() =>
        Dispatcher.UIThread.InvokeAsync(() => new StateSnapshot(
            Language: Localizer.Instance.CurrentLanguage,
            ThemeVariant: Application.Current?.RequestedThemeVariant?.Key?.ToString() ?? "Default",
            ClicksAllowed: allowClicks,
            Windows: Desktop?.Windows
                .Select(w => new WindowInfo(
                    w.Title ?? "", w.GetType().Name,
                    w.Bounds.Width, w.Bounds.Height, w.IsActive))
                .ToList() ?? [])).GetTask();

    public Task<IReadOnlyList<string>> ListElementsAsync(string? window) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var w = ResolveWindow(window);
            IReadOnlyList<string> names = w is null
                ? []
                : [.. w.GetVisualDescendants().OfType<Control>()
                        .Select(c => c.Name)
                        .OfType<string>()
                        .Distinct()
                        .OrderBy(n => n, StringComparer.Ordinal)];
            return names;
        }).GetTask();

    public Task SetLanguageAsync(string code) =>
        Dispatcher.UIThread.InvokeAsync(() => Localizer.Instance.SetLanguage(code)).GetTask();

    public Task SetThemeAsync(string variant) =>
        Dispatcher.UIThread.InvokeAsync(() => ThemeManager.Instance.Apply(variant)).GetTask();

    /// <summary>
    /// Öffnet ein Fenster nicht-modal. Bewusst <c>Show</c> statt <c>ShowDialog</c>: ein modaler
    /// Dialog würde den Dispatcher-Aufruf bis zum Schließen blockieren, und die HTTP-Antwort
    /// käme nie.
    /// </summary>
    public Task<bool> OpenWindowAsync(string name) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (BuildWindow(name) is not { } win) return false;
            win.Show(Desktop?.MainWindow!);
            return true;
        }).GetTask();

    public Task<bool> CloseWindowAsync(string nameOrTopmost) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var target = string.Equals(nameOrTopmost, "topmost", StringComparison.OrdinalIgnoreCase)
                ? Desktop?.Windows.LastOrDefault(w => w != Desktop.MainWindow)
                : ResolveWindow(nameOrTopmost);

            // Das Hauptfenster bleibt: es zu schließen beendet die App und damit die API.
            if (target is null || target == Desktop?.MainWindow) return false;
            target.Close();
            return true;
        }).GetTask();

    /// <summary>Namen der Fenster, die <c>/open</c> bauen kann — für die 404-Antwort.</summary>
    public static IReadOnlyList<string> OpenableWindows => ["info", "onboarding"];

    public Task<ClickResult> ClickAsync(string elementId) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!allowClicks) return ClickResult.NotEnabled;
            if (!DestructiveGuard.IsAllowed(elementId)) return ClickResult.Blocked;
            if (FindControl(elementId) is not { } c) return ClickResult.NotFound;

            // Zwei Zweige: Buttons mit gebundenem Command, und Buttons mit Click-Handler im
            // Code-Behind — typisch alle Dialog-Schaltflächen. Ohne den zweiten Zweig lassen
            // sich Dialoge öffnen, aber nicht wieder schließen.
            switch (c)
            {
                case Button { Command: { } cmd } b when cmd.CanExecute(b.CommandParameter):
                    cmd.Execute(b.CommandParameter);
                    return ClickResult.Ok;
                case Button btn:
                    btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return ClickResult.Ok;
                default:
                    return ClickResult.NotClickable;
            }
        }).GetTask();

    public Task<byte[]> ScreenshotAsync(string? target) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var w = ResolveWindow(target) ?? Desktop?.MainWindow;
            if (w is null || w.Bounds.Width <= 0 || w.Bounds.Height <= 0) return [];

            var scale = w.RenderScaling <= 0 ? 1.0 : w.RenderScaling;
            var px = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(w.Bounds.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(w.Bounds.Height * scale)));

            using var rtb = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
            rtb.Render(w);

            using var ms = new MemoryStream();
            // Save(Stream, int?) ist deprecated — Encoder-Options nehmen.
            rtb.Save(ms, new PngBitmapEncoderOptions());
            return ms.ToArray();
        }).GetTask();

    // ---- Helfer, laufen immer schon auf dem UI-Thread ----

    private static Window? ResolveWindow(string? key)
    {
        if (Desktop is null) return null;

        if (string.IsNullOrWhiteSpace(key) || key.Equals("active", StringComparison.OrdinalIgnoreCase))
            return Desktop.Windows.FirstOrDefault(w => w.IsActive) ?? Desktop.MainWindow;
        if (key.Equals("main", StringComparison.OrdinalIgnoreCase))
            return Desktop.MainWindow;
        if (key.Equals("topmost", StringComparison.OrdinalIgnoreCase))
            return Desktop.Windows.LastOrDefault();

        return Desktop.Windows.FirstOrDefault(w =>
                   string.Equals(w.Title, key, StringComparison.OrdinalIgnoreCase))
               ?? Desktop.Windows.FirstOrDefault(w =>
                   w.GetType().Name.Contains(key, StringComparison.OrdinalIgnoreCase));
    }

    private static Control? FindControl(string elementId)
    {
        foreach (var w in Desktop?.Windows ?? (IReadOnlyList<Window>)[])
        {
            if (w.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == elementId) is { } hit)
                return hit;
        }
        return null;
    }

    /// <summary>
    /// Baut die Fenster, die sich ohne Datenkontext sinnvoll ansehen lassen. Die übrigen
    /// Dialoge brauchen ViewModels mit geladenen Kalenderdaten — die baut man nicht aus einem
    /// HTTP-Aufruf zusammen, ohne dass der Screenshot etwas anderes zeigt als die echte App.
    /// </summary>
    private static Window? BuildWindow(string name)
    {
        if (Desktop?.MainWindow?.DataContext is not MainWindowViewModel main) return null;

        return name.ToLowerInvariant() switch
        {
            "info" or "about" => new InfoDialog { DataContext = main.CreateInfo() },
            "onboarding" => new OnboardingDialog { DataContext = main.CreateOnboarding() },
            _ => null,
        };
    }
}

/// <summary>Ausgang eines <c>/click</c> — die API bildet das auf HTTP-Codes ab.</summary>
public enum ClickResult
{
    Ok,
    NotEnabled,
    Blocked,
    NotFound,
    NotClickable,
}
