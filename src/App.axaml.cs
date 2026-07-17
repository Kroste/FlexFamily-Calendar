using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.Services.Api;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.ViewModels.Mobile;
using FlexFamilyCalendar.Views;
using FlexFamilyCalendar.Views.Mobile;

namespace FlexFamilyCalendar;

public partial class App : Application
{
    /// <summary>Vom Browser-Head vor dem Start gesetzt (localStorage-Interop). Auf Desktop unbenutzt.</summary>
    public static IBrowserKeyValueStore? BrowserStore { get; set; }

    /// <summary>Vom Browser-Head vor dem Start gesetzt (window.location.origin). Auf Desktop unbenutzt.</summary>
    public static string? BrowserOrigin { get; set; }

    /// <summary>Plattform-Backend für modale Dialoge — Desktop nutzt Window, Browser ein Overlay.</summary>
    public static IDialogService? DialogService { get; set; }

    /// <summary>true, wenn wir auf Android laufen (SingleView, aber mit Dateisystem — anders als WASM).</summary>
    public static bool IsAndroid { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            InitializeDesktop(desktop);
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            if (IsAndroid) InitializeAndroid(singleView);
            else           InitializeBrowser(singleView);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Android-Init — SingleView-Lifetime wie Browser, aber mit Dateisystem-Storage (Isolated
    /// Storage im App-Bundle) und dadurch persistenten AppSettings + gemerktem Token. Nutzt für's
    /// Erste dieselbe MainView wie der Desktop; Mobile-optimierte Views kommen in Folge-Tags.
    /// </summary>
    private void InitializeAndroid(ISingleViewApplicationLifetime singleView)
    {
        var localStorage = new StorageService();
        SecretService.Initialize(StorageService.DataDirectory);
        var settings = Task.Run(() => localStorage.LoadSettingsAsync()).GetAwaiter().GetResult();

        // Speicher-Modus: erst mal wie am Desktop (lokal oder Server je nach AppSettings).
        IStorageService storage = localStorage;
        ApiClient? apiClient = null;
        if (settings.UseServer && !string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            apiClient = new ApiClient(settings.ServerUrl);
            storage = new ApiStorageService(apiClient, localStorage);
            LogService.Info("Speicher-Modus: Server ({0}, Android)", settings.ServerUrl);
        }
        else
        {
            LogService.Info("Speicher-Modus: lokal (Android)");
        }

        var auth = new AuthService(storage, apiClient);
        var notifications = new NotificationService(storage);
        IMailSender mailSender = apiClient is not null
            ? new ApiMailSender(apiClient)
            : new LocalMailSender(localStorage);

        var loginVm = new LoginViewModel(auth);
        var hasUsers = Task.Run(() => auth.HasAnyUsersAsync()).GetAwaiter().GetResult();
        loginVm.IsFirstRun = !hasUsers;

        // KI im Android-Head: alle Provider verfügbar wie am Desktop, da Dateisystem und HttpClient
        // vorhanden sind (im Gegensatz zum Browser-Head).
        var aiService = new AiService(new IAiProvider[]
        {
            new GeminiProvider(),
            new OpenAiProvider(),
            new AnthropicProvider(),
            new PerplexityProvider(),
            new LlamaProvider()
        });
        aiService.ApplySettings(settings);

        var mainVm = new MainWindowViewModel(auth, storage, notifications, aiService, mailSender, loginVm);

        if (hasUsers)
        {
            var remembered = Task.Run(() => auth.GetRememberedUserAsync()).GetAwaiter().GetResult();
            if (remembered != null) mainVm.AutoLogin(remembered);
        }

        var mobileVm = new MobileMainViewModel(mainVm, storage, auth);
        singleView.MainView = new MobileMainView { DataContext = mobileVm };
        LogService.Info("Android-Head gestartet.");
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
        // Mail-Backend folgt dem Speicher-Modus: Server → API-Endpoint, Lokal → SMTP-Versand direkt.
        IMailSender mailSender = apiClient is not null
            ? new ApiMailSender(apiClient)
            : new LocalMailSender(localStorage);
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

        var mainVm = new MainWindowViewModel(auth, storage, notifications, aiService, mailSender, loginVm);

        // Auto-Login: gemerkten Benutzer vor dem Anzeigen anmelden (kein Login-Screen-Flackern)
        if (hasUsers)
        {
            var remembered = Task.Run(() => auth.GetRememberedUserAsync()).GetAwaiter().GetResult();
            if (remembered != null)
                mainVm.AutoLogin(remembered);
        }

        var mainWindow = new MainWindow { DataContext = mainVm };
        desktop.MainWindow = mainWindow;
        DialogService = new WindowDialogService(mainWindow);
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
        IMailSender mailSender = new ApiMailSender(apiClient);
        var loginVm = new LoginViewModel(auth) { IsFirstRun = false };   // Server hat immer den Erst-Admin

        // KI im Browser nur über den Server-Proxy — Schlüssel liegen ENV-seitig im Server.
        // Lokales Llama gibt's hier nicht; der Browser kann nicht auf den Localhost des Users.
        var aiService = new AiService(new IAiProvider[]
        {
            new ApiAiProvider(apiClient, "Gemini"),
            new ApiAiProvider(apiClient, "ChatGPT"),
            new ApiAiProvider(apiClient, "Anthropic"),
            new ApiAiProvider(apiClient, "Perplexity")
        });
        aiService.ApplySettings(settings);

        var mainVm = new MainWindowViewModel(auth, storage, notifications, aiService, mailSender, loginVm);

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
