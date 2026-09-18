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

        // Wie App.axaml: FluentTheme mit der warmen Palette, dazu die eigenen Styles und das
        // Fenster-Template (Titelleiste) — sonst prüft ein Test ein anderes Aussehen als die App.
        Styles.Add(new StyleInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Styles/FluentPalette.axaml") });
        Styles.Add(new StyleInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Styles/AppStyles.axaml") });
        Styles.Add(new StyleInclude(self)
        { Source = new Uri("avares://FlexFamilyCalendar/Views/ChromeWindow.axaml") });
    }
}

/// <summary>
/// EINE Headless-Session für den ganzen Testprozess, egal wie viele Klassen die Fixture ziehen.
/// Vorher startete jede Klasse ihre eigene — mit eigenem UI-Thread. Steuerelemente aus Klasse A
/// gehörten damit einem anderen Thread als der Dispatcher von Klasse B, und ein Sprachwechsel
/// über die statischen <c>LocalizedString</c>-Wrapper erreichte beide: „The calling thread
/// cannot access this object". Ob es krachte, hing davon ab, ob der GC die geschlossenen
/// Fenster schon eingesammelt hatte — lokal im Debug-Build grün, im Release-Build und auf der
/// CI rot.
///
/// Bewusst OHNE <c>IDisposable</c>: <c>HeadlessUnitTestSession.Dispose()</c> wartet per
/// <c>_dispatchTask.Wait()</c> auf das Ende der Dispatcher-Schleife, und die kommt hier nicht
/// zurück — der Testprozess lief danach endlos weiter, obwohl alle Tests längst grün waren
/// (sichtbar nur als Lauf ohne Zusammenfassung, nicht als Fehler). Die Session sitzt auf einem
/// Thread-Pool-Thread, ist also Hintergrund und hält den Prozess beim Beenden nicht auf.
/// </summary>
public sealed class HeadlessAppFixture
{
    private static readonly Lazy<HeadlessUnitTestSession> Shared =
        new(() => HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp)), LazyThreadSafetyMode.ExecutionAndPublication);

    public HeadlessUnitTestSession Session => Shared.Value;
}
