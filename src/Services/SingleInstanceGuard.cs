using System.IO.Pipes;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Verhindert, dass die App zweimal läuft. Ein Zweitstart meldet sich bei der bestehenden
/// Instanz, die daraufhin ihr Fenster nach vorn holt, und beendet sich selbst.
///
/// Für FlexFamily ist das aus zwei Gründen nötig: seit dem System-Tray würde ein zweiter
/// Prozess ein zweites Tray-Icon aufhängen, und im lokalen Speicher-Modus schreiben beide
/// Prozesse in dieselben JSON-Dateien unter <see cref="StorageService.DataDirectory"/> —
/// zwei Instanzen überschreiben sich gegenseitig die Kalendertage.
///
/// Umsetzung über eine Named Pipe: .NET bildet die unter Linux und macOS auf ein
/// Unix-Domain-Socket in <c>/tmp/CoreFxPipe_&lt;name&gt;</c> ab, das Muster ist also
/// plattformübergreifend. Der Name enthält den Benutzernamen — sonst blockieren sich
/// verschiedene Benutzer auf demselben Rechner gegenseitig.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const byte ActivationByte = (byte)'A';
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private NamedPipeServerStream? _server;
    private bool _disposed;

    public SingleInstanceGuard(string? userName = null)
        => _pipeName = $"FlexFamilyCalendar.SingleInstance.{userName ?? Environment.UserName}";

    /// <summary>Wird gefeuert, wenn ein Zweitstart die Aktivierung anfordert (auf einem Threadpool-Thread).</summary>
    public event Action? ActivationRequested;

    /// <summary>
    /// Versucht, der einzige laufende Prozess zu werden. <c>false</c> heißt: es läuft schon einer.
    /// </summary>
    /// <remarks>
    /// Die Prüfung läuft bewusst über einen Verbindungsversuch und NICHT allein über die
    /// <see cref="IOException"/> aus dem Server-Konstruktor. Auf Windows blockiert das OS einen
    /// zweiten Server auf demselben Pipe-Namen; unter Linux und macOS bildet .NET Named Pipes auf
    /// Unix-Domain-Sockets ab und bindet die vorhandene Socket-Datei einfach neu — der Zweitstart
    /// bekommt dort anstandslos seinen Server und hält sich für die erste Instanz.
    ///
    /// Real getroffen: mit der reinen IOException-Variante liefen zwei vollständige Instanzen
    /// samt zwei Tray-Icons nebeneinander, während die In-Process-Unit-Tests grün blieben.
    /// Ein antwortender Socket ist plattformübergreifend der verlässliche Beleg.
    /// </remarks>
    public bool TryClaim()
    {
        // Antwortet jemand, läuft die App bereits.
        if (CanConnectToPrimary()) return false;

        // Niemand antwortet. Eine trotzdem vorhandene Socket-Datei stammt aus einem Absturz —
        // unter Windows räumt das OS selbst auf, unter Linux/macOS bleibt sie liegen. Der
        // Server-Konstruktor überschreibt sie dort ohnehin; das Löschen hält nur /tmp sauber
        // und deckt den Fall ab, dass die Datei mit fremden Rechten dort liegt.
        if (!OperatingSystem.IsWindows()) TryRemoveStaleSocket();

        return TryCreateServer();
    }

    private void TryRemoveStaleSocket()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + _pipeName);
        try
        {
            if (!File.Exists(socketPath)) return;
            File.Delete(socketPath);
            LogService.Warn("Verwaistes Instanz-Socket entfernt: {0}", socketPath);
        }
        catch (Exception ex)
        {
            // Kein Abbruchgrund: der Server-Konstruktor kommt damit in aller Regel selbst klar.
            LogService.Warn("Verwaistes Instanz-Socket {0} konnte nicht entfernt werden: {1}",
                socketPath, ex.Message);
        }
    }

    /// <summary>Meldet der laufenden Instanz, dass sie sich zeigen soll. Nur nach <c>TryClaim() == false</c> sinnvoll.</summary>
    public void NotifyPrimary()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect((int)ConnectTimeout.TotalMilliseconds);
            client.WriteByte(ActivationByte);
            client.Flush();
            LogService.Info("Bereits laufende Instanz benachrichtigt — dieser Start beendet sich.");
        }
        catch (Exception ex)
        {
            // Die laufende Instanz hängt oder ist gerade beim Beenden. Kein Grund für einen
            // Fehlerdialog: der Zweitstart beendet sich so oder so.
            LogService.Warn("Laufende Instanz nicht erreichbar: {0}", ex.Message);
        }
    }

    private bool TryCreateServer()
    {
        try
        {
            _server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _cts = new CancellationTokenSource();
            _ = ListenAsync(_server, _cts.Token);
            return true;
        }
        catch (IOException)
        {
            // Pipe-Name belegt = es läuft bereits eine Instanz.
            return false;
        }
    }

    private bool CanConnectToPrimary()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(100);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ListenAsync(NamedPipeServerStream server, CancellationToken token)
    {
        var buffer = new byte[1];
        while (!token.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(token);
                var read = await server.ReadAsync(buffer, token);
                if (read == 1 && buffer[0] == ActivationByte)
                {
                    LogService.Info("Zweitstart erkannt — bestehendes Fenster wird nach vorn geholt.");
                    ActivationRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException ex)
            {
                // Abgebrochene Verbindung: nächste abwarten, nicht die Schleife verlieren.
                LogService.Debug("Instanz-Pipe: Verbindung abgebrochen ({0})", ex.Message);
            }

            try
            {
                if (server.IsConnected) server.Disconnect();
            }
            catch (Exception ex)
            {
                LogService.Debug("Instanz-Pipe konnte nicht getrennt werden: {0}", ex.Message);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _cts?.Cancel(); } catch (ObjectDisposedException) { /* schon weg */ }
        _cts?.Dispose();
        _server?.Dispose();
        _cts = null;
        _server = null;
    }
}
