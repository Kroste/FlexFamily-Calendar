using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Minimal-Anwendung für die Headless-UI-Tests. Bewusst NICHT die echte <c>App</c>: deren
/// <c>OnFrameworkInitializationCompleted</c> baut Fenster, Tray und Storage auf, was im Test
/// weder nötig noch möglich ist.
///
/// Die Ressourcen der App müssen dagegen vollständig mit — und zwar nicht nur fürs Aussehen:
/// ein Control, dessen Hintergrund über einen toten <c>DynamicResource</c> läuft, bekommt in
/// Avalonia gar keinen Hintergrund und ist damit auch nicht mehr hit-testbar. Ohne
/// <c>Palette.axaml</c> war der „+"-Knopf der Tageszelle im Test unsichtbar für Klicks: der
/// Zeiger fiel durch ihn hindurch auf den Eintrags-Chip darunter, und der Test prüfte
/// klaglos den falschen Pfad.
/// </summary>
public class HeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>()
                     .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        var self = new Uri("avares://FlexFamilyCalendar/");

        Resources.MergedDictionaries.Add(new ResourceInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Styles/Icons.axaml") });
        Resources.MergedDictionaries.Add(new ResourceInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Styles/Palette.axaml") });

        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Styles/AppStyles.axaml") });
    }
}

/// <summary>
/// Hält die Headless-Session über alle Tests einer Klasse. Der Aufbau kostet spürbar Zeit und
/// die Session ist prozessweit — pro Test eine neue wäre langsam und würde sich gegenseitig stören.
///
/// Bewusst OHNE <c>IDisposable</c>: <c>HeadlessUnitTestSession.Dispose()</c> wartet per
/// <c>_dispatchTask.Wait()</c> auf das Ende der Dispatcher-Schleife, und die kommt hier nicht
/// zurück — der Testprozess lief danach endlos weiter, obwohl alle Tests längst grün waren
/// (sichtbar nur als Lauf ohne Zusammenfassung, nicht als Fehler). Die Session sitzt auf einem
/// Thread-Pool-Thread, ist also Hintergrund und hält den Prozess beim Beenden nicht auf.
/// </summary>
public sealed class HeadlessAppFixture
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));
}
