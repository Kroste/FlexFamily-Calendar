using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.Services.Api;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;

namespace FlexFamilyCalendar;

public partial class App : Application
{
    /// <summary>Vom Browser-Head vor dem Start gesetzt (localStorage-Interop). Auf Desktop unbenutzt.</summary>
    public static IBrowserKeyValueStore? BrowserStore { get; set; }

    /// <summary>Vom Browser-Head vor dem Start gesetzt (window.location.origin). Auf Desktop unbenutzt.</summary>
    public static string? BrowserOrigin { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            InitializeDesktop(desktop);
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            InitializeBrowser(singleView);

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var localStorage = new StorageService();
        SecretService.Initialize(StorageService.DataDirectory);

        // Einstellungen sind lokale Installations-Config (enthalten u.a. die Server-URL).
        var settings = Task.Run(() => localStorage.LoadSettingsAsync()).GetAwaiter().GetResult();

        // Speicher-Modus: entweder lokal (JSON) oder Server (API) — kein Parallelbetrieb.
        IStorageService storage = localStorage;
        ApiClient? apiClient = null;
        if (settings.UseServer && !string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            apiClient = new ApiClient(settings.ServerUrl);
            storage = new ApiStorageService(apiClient, localStorage);
            LogService.Info("Speicher-Modus: Server ({0})", settings.ServerUrl);
        }
        else
        {
            LogService.Info("Speicher-Modus: lokal (JSON)");
        }

        var auth = new AuthService(storage, apiClient);
        var notifications = new NotificationService(storage);
        var loginVm = new LoginViewModel(auth);

        // Erstkonfiguration erkennen (async, aber wir warten kurz)
        var hasUsers = Task.Run(() => auth.HasAnyUsersAsync()).GetAwaiter().GetResult();
        loginVm.IsFirstRun = !hasUsers;

        var aiService = new AiService(new IAiProvider[]
        {
            new GeminiProvider(),
            new OpenAiProvider(),
            new AnthropicProvider(),
            new PerplexityProvider(),
            new LlamaProvider()
        });

        aiService.ApplySettings(settings);

        var mainVm = new MainWindowViewModel(auth, storage, notifications, aiService, loginVm);

        // Auto-Login: gemerkten Benutzer vor dem Anzeigen anmelden (kein Login-Screen-Flackern)
        if (hasUsers)
        {
            var remembered = Task.Run(() => auth.GetRememberedUserAsync()).GetAwaiter().GetResult();
            if (remembered != null)
                mainVm.AutoLogin(remembered);
        }

        desktop.MainWindow = new MainWindow { DataContext = mainVm };
    }

    private void InitializeBrowser(ISingleViewApplicationLifetime singleView)
    {
        // Browser läuft NUR im Server-Modus — kein Dateisystem, kein StorageService/SecretService.
        // Settings (Server-URL + gemerktes Token) gehen über localStorage (BrowserStore vom Head gesetzt).
        var browserStore = BrowserStore ?? new InMemoryBrowserKeyValueStore();
        var settingsStore = new BrowserSettingsStorage(browserStore);
        // BrowserSettingsStorage.LoadSettingsAsync ist in-memory → Task ist abgeschlossen, kein WASM-Deadlock.
        var settings = settingsStore.LoadSettingsAsync().GetAwaiter().GetResult();

        // HttpClient.BaseAddress MUSS absolut sein — relative "/" wirft im URI-Konstruktor bzw. löst
        // unter file:// fatal auf. Fallback-Reihenfolge: explizite ServerUrl > Browser-Origin > "/".
        var origin = BrowserOrigin?.TrimEnd('/');
        var serverUrl = !string.IsNullOrWhiteSpace(settings.ServerUrl)
            ? settings.ServerUrl
            : (!string.IsNullOrEmpty(origin) ? origin! : "/");

        if (origin is not null && origin.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            LogService.Warn("SPA wurde via file:// geöffnet — API-Aufrufe schlagen fehl. Bitte über Caddy/HTTPS starten.");

        var apiClient = new ApiClient(serverUrl);
        var storage = new ApiStorageService(apiClient, settingsStore);
        var auth = new AuthService(storage, apiClient);
        var notifications = new NotificationService(storage);
        var loginVm = new LoginViewModel(auth) { IsFirstRun = false };   // Server hat immer den Erst-Admin

        var aiService = new AiService(new IAiProvider[]
        {
            new GeminiProvider(),
            new OpenAiProvider(),
            new AnthropicProvider(),
            new PerplexityProvider(),
            new LlamaProvider()
        });
        aiService.ApplySettings(settings);

        var mainVm = new MainWindowViewModel(auth, storage, notifications, aiService, loginVm);

        // View sofort setzen (Login-Screen), Auto-Login per Token läuft asynchron — würde sonst in WASM blockieren.
        singleView.MainView = new MainView { DataContext = mainVm };

        LogService.Info("Speicher-Modus: Server (Browser, {0})", serverUrl);

        _ = AutoLoginBrowserAsync(auth, mainVm);
    }

    private static async Task AutoLoginBrowserAsync(AuthService auth, MainWindowViewModel mainVm)
    {
        try
        {
            var user = await auth.GetRememberedUserAsync();
            if (user is not null) mainVm.AutoLogin(user);
        }
        catch (Exception ex)
        {
            LogService.Warn("Auto-Login (Browser) fehlgeschlagen: {0}", ex.Message);
        }
    }
}
