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
    private readonly object _serverLock = new();
    private CancellationTokenSource? _cts;
    private NamedPipeServerStream? _server;
    private bool _disposed;
    private int _acceptFailures;

    /// <summary>Anzahl fehlgeschlagener Annahmen seit dem Start — für Tests, die eine
    /// Endlosschleife ausschließen müssen, ohne sie auf der Uhr zu messen.</summary>
    internal int AcceptFailures => Volatile.Read(ref _acceptFailures);

    /// <summary>Ab so vielen Fehlern in Folge wird die Pipe-Instanz ersetzt statt zurückgesetzt.</summary>
    private const int RecreateAfterFailures = 5;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(5);

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
            _server = NewServer();
            _cts = new CancellationTokenSource();
            _ = ListenAsync(_cts.Token);
            return true;
        }
        catch (IOException)
        {
            // Pipe-Name belegt = es läuft bereits eine Instanz.
            return false;
        }
    }

    private NamedPipeServerStream NewServer()
        => new(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

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

    private async Task ListenAsync(CancellationToken token)
    {
        var buffer = new byte[1];
        var failuresInARow = 0;
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server;
            lock (_serverLock) server = _server;
            if (server is null) return;

            try
            {
                await server.WaitForConnectionAsync(token);
                var read = await server.ReadAsync(buffer, token);
                if (read == 1 && buffer[0] == ActivationByte)
                {
                    LogService.Info("Zweitstart erkannt — bestehendes Fenster wird nach vorn geholt.");
                    ActivationRequested?.Invoke();
                }
                failuresInARow = 0;
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
                failuresInARow++;
                Interlocked.Increment(ref _acceptFailures);
                // Gedrosselt: nur der erste Fehler einer Serie landet im Log. Vorher stand hier
                // jede Iteration — unter Windows im Millisekundentakt, bis die Platte voll war.
                if (failuresInARow == 1)
                    LogService.Debug("Instanz-Pipe: Verbindung abgebrochen ({0})", ex.Message);
            }

            if (!ResetForNextClient(server) || failuresInARow >= RecreateAfterFailures)
            {
                if (!ReplaceServer(server, failuresInARow)) return;
            }

            if (failuresInARow > 0)
            {
                // Selbst wenn künftig ein anderer Fehlerpfad die Pipe kaputt lässt: eine
                // ungebremste Schleife ist damit ausgeschlossen.
                try { await Task.Delay(Backoff(failuresInARow), token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>
    /// Bereitet die Pipe auf den nächsten Client vor.
    ///
    /// <c>Disconnect()</c> läuft bewusst OHNE Vorabprüfung von <c>IsConnected</c>. Legt ein
    /// Client auf, ohne zu schreiben — genau das tut die Probe jedes Zweitstarts in
    /// <see cref="CanConnectToPrimary"/> —, geht die Server-Pipe unter Windows in den Zustand
    /// <c>Broken</c>, und dort ist <c>IsConnected</c> false. Die frühere Prüfung übersprang
    /// deshalb genau den Fall, in dem das Trennen Pflicht ist: ohne <c>Disconnect()</c> wirft
    /// jedes weitere <c>WaitForConnectionAsync</c> sofort „Pipe is broken", und die Schleife lief
    /// ohne Pause weiter — im Millisekundentakt, mit einem Log-Eintrag pro Durchlauf.
    /// Unter Linux bildet .NET die Pipe auf ein Unix-Socket ab, dort trat das nie auf.
    /// </summary>
    /// <returns><c>false</c>, wenn die Instanz sich nicht zurücksetzen ließ und ersetzt werden muss.</returns>
    private static bool ResetForNextClient(NamedPipeServerStream server)
    {
        try
        {
            server.Disconnect();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return true;   // Dispose läuft, die Schleife endet im nächsten Durchlauf
        }
        catch (InvalidOperationException)
        {
            // Nie verbunden gewesen oder schon getrennt — die Pipe wartet bereits.
            return true;
        }
        catch (Exception ex)
        {
            LogService.Debug("Instanz-Pipe ließ sich nicht zurücksetzen: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>Letzte Rettung: alte Pipe-Instanz wegwerfen, neue anlegen.</summary>
    /// <returns><c>false</c>, wenn der Wächter aufgibt (Dispose läuft oder Name inzwischen belegt).</returns>
    private bool ReplaceServer(NamedPipeServerStream broken, int failuresInARow)
    {
        lock (_serverLock)
        {
            if (_disposed || !ReferenceEquals(_server, broken)) return !_disposed;

            LogService.Warn("Instanz-Pipe nach {0} Fehlern in Folge neu aufgebaut.", failuresInARow);
            broken.Dispose();
            try
            {
                _server = NewServer();
                return true;
            }
            catch (Exception ex)
            {
                // Zwischen Dispose und Neuanlage hat ein anderer Prozess den Namen belegt. Die
                // App läuft weiter, nur holt ein Zweitstart sie nicht mehr nach vorn.
                LogService.Warn("Instanz-Pipe konnte nicht neu angelegt werden: {0}", ex.Message);
                _server = null;
                return false;
            }
        }
    }

    private static TimeSpan Backoff(int failuresInARow)
    {
        var ms = 50 * Math.Pow(2, Math.Min(failuresInARow - 1, 10));
        return TimeSpan.FromMilliseconds(Math.Min(ms, MaxBackoff.TotalMilliseconds));
    }

    public void Dispose()
    {
        lock (_serverLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try { _cts?.Cancel(); } catch (ObjectDisposedException) { /* schon weg */ }
        _cts?.Dispose();
        _cts = null;
        lock (_serverLock)
        {
            _server?.Dispose();
            _server = null;
        }
    }
}
