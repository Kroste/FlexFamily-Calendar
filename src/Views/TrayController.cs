using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Views;

/// <summary>
/// System-Tray nach Kroste-Muster: <b>Minimieren legt ins Tray, Schließen beendet regulär.</b>
///
/// Weil nur <see cref="Window.Hide"/> aufgerufen wird und nicht geschlossen, ist kein
/// Umbau des <c>ShutdownMode</c> nötig — das braucht nur die Variante „Schließen → Tray".
///
/// Drei Absicherungen, die das Muster verlangt:
/// <list type="bullet">
/// <item>Die App hält diesen Controller als Feld. Ohne die Referenz sammelt der GC das
///       TrayIcon ein, und es verschwindet nach einiger Laufzeit „von selbst".</item>
/// <item>Restore läuft über <see cref="Dispatcher.UIThread"/> mit Guard-Flag: das Setzen von
///       <c>WindowState.Normal</c> feuert den Listener erneut, sonst entsteht eine
///       Minimize/Restore-Schleife.</item>
/// <item>Das Setup steckt in try/catch. Auf minimalen Desktops oder mit kaputtem DBus gibt es
///       keinen Tray — dann verhält sich Minimieren normal und die App bleibt voll nutzbar.</item>
/// </list>
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly Window _window;
    private TrayIcon? _trayIcon;
    private bool _restoreInProgress;

    /// <summary>true, wenn der Tray steht — sonst bleibt Minimieren das normale OS-Verhalten.</summary>
    public bool IsActive => _trayIcon is not null;

    public TrayController(Application app, Window window)
    {
        _window = window;

        try
        {
            _trayIcon = new TrayIcon
            {
                Icon = LoadIcon(),
                ToolTipText = "FlexFamily Calendar",
                IsVisible = true,
                Menu = BuildMenu()
            };
            _trayIcon.Clicked += (_, _) => Restore();

            TrayIcon.SetIcons(app, [_trayIcon]);
            _window.PropertyChanged += OnWindowPropertyChanged;

            LogService.Info("System-Tray aktiv.");
        }
        catch (Exception ex)
        {
            // Kein Tray verfügbar: das ist kein Fehlerfall für den Nutzer, nur eine
            // Einschränkung. Minimieren verhält sich dann wie ohne Tray.
            _trayIcon = null;
            LogService.Warn("System-Tray nicht verfügbar ({0}) — Minimieren bleibt Standardverhalten.",
                ex.Message);
        }
    }

    private NativeMenu BuildMenu()
    {
        var show = new NativeMenuItem("Anzeigen");
        show.Click += (_, _) => Restore();

        var quit = new NativeMenuItem("Beenden");
        quit.Click += (_, _) =>
        {
            LogService.Info("Beenden über das Tray-Menü.");
            _window.Close();
        };

        return [show, new NativeMenuItemSeparator(), quit];
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://FlexFamilyCalendar/Assets/flexfamily-calendar.png");
            return new WindowIcon(Avalonia.Platform.AssetLoader.Open(uri));
        }
        catch (Exception ex)
        {
            // Ohne Icon zeigt der Tray einen Platzhalter — besser als gar kein Tray.
            LogService.Warn("Tray-Icon konnte nicht geladen werden: {0}", ex.Message);
            return null;
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_restoreInProgress) return;
        if (e.GetNewValue<WindowState>() != WindowState.Minimized) return;

        LogService.Debug("Fenster minimiert — ab ins Tray.");
        _window.Hide();
    }

    /// <summary>Holt das Fenster aus dem Tray zurück und gibt ihm den Fokus.</summary>
    public void Restore()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _restoreInProgress = true;
            try
            {
                _window.Show();
                _window.WindowState = WindowState.Normal;
                _window.Activate();
            }
            finally
            {
                _restoreInProgress = false;
            }
        });
    }

    public void Dispose()
    {
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
