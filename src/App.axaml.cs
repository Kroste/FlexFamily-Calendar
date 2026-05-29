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
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
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

        base.OnFrameworkInitializationCompleted();
    }
}
