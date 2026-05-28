using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
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
            var storage = new StorageService();
            SecretService.Initialize(StorageService.DataDirectory);

            var auth = new AuthService(storage);
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

            var mainVm = new MainWindowViewModel(auth, storage, notifications, loginVm);

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
