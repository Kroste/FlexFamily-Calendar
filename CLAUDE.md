# CLAUDE.md

> Diese Datei wird von Claude Code / Copilot beim Session-Start als Kontext geladen.
> Der **projektübergreifende Kanon** steht in `../CLAUDE.md` (Master-Vorlage) und gilt
> unverändert weiter. Diese Datei füllt nur den Abschnitt *„Projekt"* aus und ergänzt
> die projektspezifischen Besonderheiten.

---

## Arbeitsweise

**Deal:** Lars liefert die Ideen, Claude setzt um.

- Sprache: **Deutsch**, immer **„du"**, nie „Sie".
- Antwortstil: direkt, technisch tief, klare Single-Path-Empfehlung mit Begründung.
- Rückfragen als **Text** stellen (nicht über den Frage-Dialog).
- Für alles Sinnvolle **Tests** schreiben (xUnit unter `tests/`) — kein Feature gilt ohne als fertig.
- **Iterativ:** pro „weiter" ein Feature — Plan, Rückfragen, testbare Engine, UI, kleine Commits.
- **Git:** nach jedem Schritt committen + `push origin main` (direkt auf `main`).
- **Nach jedem Tag Actions prüfen** (`gh run list`) und Failures sofort reparieren.

---

## Projekt

- **Name:** `FlexFamily Calendar`
- **Kurzbeschreibung:** Familienplaner für Arbeitszeiten, Schichten, Aktivitäten (Schule/Kita/Sport),
  Krankmeldungen, Urlaubswünsche und Schichttausch — für Eltern, Kinder, Angestellte und Au-Pairs.
- **Repository:** `https://github.com/Kroste/FlexFamily-Calendar`
- **Lokaler Pfad:** `/home/OsteL/Entwicklung/FlexFamily Calendar`
- **Live:** `https://flexfamily.cloud` (Hostinger-VPS, Debian 13, Docker Compose:
  Postgres 17 + ASP.NET-Core-API + Caddy-Reverse-Proxy mit HTTPS/Let's Encrypt +
  Watchtower für Auto-Updates).

### Projektspezifische Besonderheiten

- **Ein Codebase, vier Heads:** Avalonia 12.1 / .NET 10 → **Desktop** (`desktop/`, Linux/Windows),
  **Browser/WASM** (`browser/`), **Android** (`mobile/`), plus die geteilte Client-Bibliothek
  (`src/`). Alle Heads teilen ViewModels, Services, API-Clients; die Views sind pro Head auf
  das Bedien-Setting zugeschnitten.
- **Android-Fläche bewusst reduziert:** Login, Wochenplan-Ansicht (Tag-Karten, nur eigene
  Einträge), Krank/Urlaub eintragen, Schichttausch. **Kein Admin-Bereich, kein PDF/Mail/KI,
  keine Wochenübersicht/Profil-Editor** auf dem Handy — die macht der Nutzer weiter am
  Desktop oder im Web.
- **Speicher-Modus = entweder/oder, kein Parallelbetrieb, kein Sync:** Die App läuft **entweder**
  lokal gegen JSON-Dateien (`StorageService`) **oder** gegen die Server-API (`ApiStorageService`).
  `IStorageService` ist die Naht. `AppSettings.UseServer` + `ServerUrl` (lokale Installations-Config,
  **keine** Domänendaten) schalten um. Android startet per Default gegen `flexfamily.cloud`.
  - **Im Server-Modus MUSS alles über die API laufen — kein lokaler Fallback.** Fehlende Server-Flächen
    werden autonom als Endpunkte nachgezogen, nicht aus lokalen Dateien bedient.
- **Server = Single Source of Truth + Sicherheitsgrenze:** Authn/Authz und Privatsphäre-Maskierung
  werden **serverseitig** erzwungen (Client nie vertrauen). JWT-Login, Rollen (Admin/User) und
  Kategorien (Parent/Child/Employee/AuPair).
- **Sicht-Regel (v0.1.32):** Nicht-Admins sehen **fremde Einträge erst nach Freigabe des Tages**
  durch den Admin (finalisierte Wochen). Eigene Krank/Urlaub-Wünsche und Aktivitäten immer,
  eigene Work-Schicht (kommt vom Admin) erst nach Freigabe. Admin sieht alles; Impersonate-
  Sicht clientseitig nachgefiltert (`EntriesVisibleUnderImpersonation`).
- **Urlaubswunsch-Approval:** Nicht-Admin-Urlaub landet als `Status=Pending`, ist beim
  Antragsteller im Kalender grau/durchscheinend mit „(Wunsch, wartet auf Bestätigung)"
  markiert. Der Server erzeugt bei jedem Admin eine Benachrichtigung; im Notifications-Dialog
  gibt es grün „Genehmigen"/rot „Ablehnen" direkt an der Zeile.
- **KI ist additiv, nie im kritischen Pfad** — die App funktioniert ohne KI voll. Im Server-Modus
  liegt der API-Key serverseitig in ENV (`ApiAiProvider`), lokal über die Einstellungen.
- **PDF-Export:** eigener abhängigkeitsfreier PDF-Writer (reines Managed). **Keine native
  PDF-/Skia-Lib hinzufügen** — QuestPDF kollidiert in-process mit Avalonias SkiaSharp.
- **Lokale JSON-Ablage läuft ausschließlich über `JsonFileStore`:** atomar schreiben
  (`.tmp` + `File.Move(overwrite)`) und defekte Dateien nach `.broken` sichern statt sie beim
  nächsten Save zu überschreiben. Quarantäne **nur** bei `JsonException`, nicht bei
  `IOException` — bei gesperrter Datei ist der Inhalt intakt, ein Verschieben würde gute Daten
  wegräumen.
- **Keine Farbliterale im XAML.** Alle Farben liegen als Rollen-Keys in
  `src/Styles/Palette.axaml` (`DangerBrush`, `SuccessBrush`, `ScrimBrush`, …). Tote
  `{DynamicResource …}`-Verweise scheitern in Avalonia **still** — das Element rendert einfach
  falsch. `ResourceKeyTests` gleicht referenzierte gegen definierte Keys ab und schlägt bei
  neuen Literalen an; Fluent-Keys sind am `System`-Präfix erkennbar und ausgenommen.
  **Oberflächen-Rollen liegen in `ThemeDictionaries` (Light/Dark)**, weil die App Hell/Dunkel/
  System anbietet — der Kroste-Look ist fix dunkel und wäre hier unbrauchbar. Eine Rolle, die
  nur in einem der beiden Themes steht, fällt im anderen still aus; ein Test hält deshalb fest,
  dass Light und Dark denselben Satz definieren. Statusfarben (Rot, Grün, Orange) bleiben
  theme-neutral.
- **Style-Bibliothek statt Inline-Optik:** `src/Styles/AppStyles.axaml` bringt Button-Default
  plus `.accent`/`.ghost`/`.danger`, `Border.card`/`.card-flat`,
  `TextBlock.h1`/`.h2`/`.section-label`/`.muted`/`.secondary`, dünne ProgressBar und
  `Rectangle.divider-h`/`-v`. Neue Views nutzen diese Klassen; bestehende werden **beiläufig**
  nachgezogen, wenn sie ohnehin angefasst werden — der Kroste-Standard verlangt ausdrücklich
  keinen separaten Refactor-Ritt, sonst driftet der Look während des Umbaus auseinander.
  Selektoren auf Template-Interna (`/template/ Border#PART_…`) nur mit sichtbarer App
  einbauen: falsche PART-Namen scheitern still, und die Fluent-Templates liegen als
  kompiliertes XAML vor, sind also nicht nachschlagbar. Deshalb fehlt der Fokus-Ring noch.
- **Sprachwechsel über `LocalizedString`**, nicht über Indexer-Binding. Ein
  `PropertyChanged("Item[]")` ist die WPF-Konvention und wird von Avalonia 12 nur unzuverlässig
  verarbeitet: Fenster ohne Fokus bleiben stale. Die Wrapper liegen in einem **statischen**
  Cache — Avalonia hält `Binding.Source` nicht stark, ein pro Binding erzeugter Wrapper wäre
  nach dem ersten Rendering weg.
- **JSON:** Ausschließlich `System.Text.Json` mit gemeinsamem `JsonOptions.Pretty`
  (PropertyNamingPolicy=null, damit alte PascalCase-JSON-Files weiter lesbar bleiben);
  Newtonsoft.Json ist als Dependency raus.
- **Datenschutz:** Fremde Krank-/Urlaubsgründe erscheinen nur als „Abwesend" (Maskierung pro Betrachter),
  im Plan, PDF und Mail-Versand (je Empfänger aus dessen Sicht).
- **Titelleiste:** Die Drag-Fläche trägt `chrome:WindowDecorationProperties.ElementRole="TitleBar"`,
  die drei Fensterbuttons `="User"` — **nicht** `MinimizeButton`/`MaximizeButton`/`CloseButton`,
  diese Rollen laufen über den Non-Client-Pfad, wo DWM die Klicks schlucken kann. Zusätzlich
  prüft der managed `PointerPressed`-Handler über `LandedOnInteractiveChild`, ob der Klick auf
  einem interaktiven Control gelandet ist. Der Guard löst ein anderes Problem als die Rollen
  (managed Fallback statt OS-Hit-Test) und darf nicht als „dank ElementRole überflüssig"
  entfernt werden: sobald ein Umschalter oder Suchfeld in die Leiste kommt, startet sonst jeder
  Klick darauf einen Fenster-Drag und das Dropdown öffnet nie.
  `ExtendClientAreaTitleBarHeightHint` steht bewusst auf `32` statt dem im Skill genannten `-1`:
  der Wert passt exakt zur Höhe der gerenderten Leiste, und das Symptom „tote Titelleiste",
  gegen das `-1` hilft, liegt hier nicht vor.
- **Single-Instance-Guard (nur Desktop):** `SingleInstanceGuard` in `Program.Main`, **vor**
  Avalonia. Ein Zweitstart holt die laufende Instanz nach vorn und beendet sich. Nötig wegen
  Tray-Icon und weil im lokalen Modus zwei Prozesse dieselben JSON-Dateien überschreiben.
  **Die Erkennung MUSS über einen Verbindungsversuch laufen, nicht über die `IOException` des
  `NamedPipeServerStream`-Konstruktors:** unter Linux/macOS bildet .NET Named Pipes auf
  Unix-Domain-Sockets ab und bindet die Datei einfach neu — der Zweitstart bekommt dort seinen
  Server und hält sich für die erste Instanz. In-Process-Tests fangen das nicht (dort wirft der
  Konstruktor); nach Änderungen an `TryClaim` mit zwei echten Prozessen aus einem
  `dotnet publish`-Output gegenprüfen.
- **System-Tray (nur Desktop):** Minimieren legt ins Tray, Schließen beendet regulär — kein
  `ShutdownMode`-Umbau nötig, weil nur `Hide()` läuft. `TrayController` MUSS als Feld in `App`
  gehalten werden (sonst sammelt der GC das Icon ein), Restore läuft über den UI-Dispatcher mit
  Guard-Flag (sonst Minimize/Restore-Schleife), und das Setup steckt in try/catch (kein Tray auf
  minimalen Desktops / kaputtem DBus). `Tmds.DBus.Protocol` ist auf die Avalonia-Linie gepinnt.
- **UI-Hinweise:** Hover-Tooltips im Web/Desktop erklären jede Bedien-Fläche
  (`views:Hint.Text` → `HintService`). Ein/Aus-Toggle im Profil. Erst-Login zeigt eine
  4-Slide-Onboarding-Tour, danach nur bei „Später zeigen" erneut.

---

## Repo-Struktur

```
src/       Geteilte Client-Bibliothek (Models, ViewModels, Services, Views) — alle Heads teilen sich das
desktop/   Avalonia-Desktop-Head (Windows/Linux, ClassicDesktop-Lifetime)
browser/   Avalonia-Browser/WASM-Head (SingleView-Lifetime)
mobile/    Avalonia-Android-Head (Android 8+, SingleView-Lifetime, dedizierte Mobile-Views)
server/    FlexFamilyCalendar.Api (ASP.NET Core Minimal API, EF Core, Postgres)
tests/     FlexFamilyCalendar.Tests (Client) + FlexFamilyCalendar.Api.Tests (Server) — xUnit
docs/      Screenshots, Logo
```

- **Central Package Management:** alle Paketversionen stehen in `Directory.Packages.props`,
  die csproj referenzieren **ohne** `Version`-Attribut. Avalonia MUSS über alle vier Heads
  auf derselben Linie bleiben — Dependabot gruppiert die Pakete deshalb zu einem PR.
  Nach jedem Avalonia-Bump gegenprüfen, dass SkiaSharp managed und native dieselbe Linie
  zeigen (`dotnet list … package --include-transitive | grep -i skia`): eine Drift bleibt
  beim Build unsichtbar und tötet die App erst beim ersten gerenderten Fenster.
- **Beide Dockerfiles kopieren `Directory.Build.props` UND `Directory.Packages.props`**
  einzeln ins Build-Image. Fehlt die zweite Datei, scheitert schon der Restore mit NU1015.
- Das **Android-Projekt (`mobile/`) ist NICHT in `FlexFamilyCalendar.slnx`** — damit
  `dotnet build/test` der Solution ohne `android`-Workload durchgeht. Der Android-Build
  läuft im Release-Workflow als eigener Job (`Android APK`, mit `dotnet workload install android`).
- **Server-DB-Schema** ändert sich über **EF-Migrationen**
  (`server/FlexFamilyCalendar.Api/Migrations/`). Nach Feldänderungen:
  `dotnet ef migrations add <Name>` — Live-DB wird beim Redeploy per Startup-Migrate
  aktualisiert (Retry-Block in `Program.cs`, im Testing-Environment übersprungen).
- **Übersetzung Client↔Server** liegt in `src/Services/Api/*Mapping.cs` (User, Entry, ActivityType,
  RecurringActivity, ShiftSwap, Notification). Eintrags-Modell-Unterschied: Desktop = Abwesenheit als
  Tag-pro-Eintrag mit `AbsenceGroupId`, Server = ein Bereich-Eintrag (Date+EndDate).

---

## Tech-Stack (Baseline)

- **.NET 10** / **C#** (LangVersion `latest`, `ImplicitUsings`, `Nullable enable`, `TreatWarningsAsErrors`)
  — zentral in `Directory.Build.props`.
- **Avalonia 12.1.0**, MVVM via **CommunityToolkit.Mvvm**, Fluent-Theme, Inter-Font.
- Alle Fenster erben von `ChromeWindow` (Custom-Chrome-Basisklasse: `WindowDecorations.BorderOnly`,
  `ExtendClientAreaToDecorationsHint=true`, `CanResize=true`, eigene Titelleiste mit Drag/Min/Max/Close).
- DI/Hosting via **Microsoft.Extensions.DependencyInjection**, Logging via **NLog**.
- Server: **ASP.NET Core Minimal API**, **EF Core** + **Npgsql/PostgreSQL**, JWT-Auth, BCrypt-Passwörter,
  `AddProblemDetails()` + `UseExceptionHandler()` als globaler Netz.
- Server-Integration-Tests via `WebApplicationFactory<Program>` + `EntityFrameworkCore.InMemory`.
  Das Api-Testprojekt referenziert `Microsoft.EntityFrameworkCore.Relational` **explizit** —
  über den Web-SDK-ProjectReference landet die DLL nicht im Test-Output.
- **Tests laufen auf xunit.v3 / Microsoft.Testing.Platform.** Dafür braucht es alle drei
  Teile: den `test`-Runner-Block in der `global.json`, `<OutputType>Exe</OutputType>` in
  beiden Testprojekten und **kein** `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio`.
  Keine VSTest-Flags an `dotnet test` hängen (`--nologo` & Co.) — die reicht es an die
  Test-Exe durch, die sie nicht kennt, und der Lauf endet mit „Es wurden keine Tests
  ausgeführt". Tests gegen globale Singletons (`Localizer`, `SecretService`) gehören in eine
  nicht-parallele Collection: xunit.v3 fixiert die Reihenfolge innerhalb einer Klasse nicht.
- Versionierung via **MinVer** (Git-Tag `vX.Y.Z`), GitHub-Account **Kroste** (`lars-oste@gmx.de`).

---

## Deploy & CI

- **CI-Workflow** (`.github/workflows/ci.yml`): auf jeden Push/PR `dotnet test FlexFamilyCalendar.slnx`
  (installiert vorher `wasm-tools` Workload für den Browser-Head).
- **Release-Workflow** (`.github/workflows/release.yml`): getriggert auf jedes Tag `vX.Y.Z` und
  baut parallel:
  - Desktop-linux-x64 (tar.gz), Desktop-win-x64 (zip), Linux-AppImage
  - Android APK (`Android APK`-Job mit `setup-java` + `setup-android`)
  - Docker-Images `flexfamily-calendar-api` und `flexfamily-calendar-caddy` (mit eingebetteter
    WASM-SPA und Caddyfile) auf Docker Hub
- **Docker-Compose-Setup** in `server/docker-compose.yml`: Postgres 17 + API + Caddy + Watchtower.
  Watchtower zieht neue `:latest`-Images automatisch (Docker-API v1.44 forciert für Kompatibilität
  mit modernen Docker-Daemons).

---

## Logging & Secrets

- **Grundsätzlich alles loggen** (Trace/Debug für Abläufe, Info für Aktionen, Warn/Error für Probleme).
  Client **und** Server laufen auf NLog mit eigener `nlog.config`: Datei ab Trace, Konsole ab Info.
  Die Server-Konsole ist das, was `docker logs` zeigt — sie darf nie wegfallen. Log-Verzeichnis
  der API über `FFC_LOG_DIR` (in Compose auf einem Volume, sonst Temp-Fallback).
- **Über die API alles loggen** (Methode/Pfad/Status/Dauer/Benutzer) — das macht die
  `RequestLoggingMiddleware`. Bewusst **ohne Body und ohne Header**: sonst stünde das Passwort
  jedes Logins und jedes JWT im Klartext im Log. In `appsettings.json` steht der Log-Level
  deshalb auf `Trace` — dieser Filter greift VOR NLog, ein `Information` würde die Trace-Regel
  der `nlog.config` wirkungslos machen.
- **Passwörter/Tokens/Secrets NIEMALS loggen** und nie im Klartext ablegen/committen
  (Desktop: `SecretService` mit AES-Keyfile, Browser: Origin-Isolation, Android: Isolated Storage
  über `SecretService`; API-Keys serverseitig in ENV). Als zweite Verteidigungslinie steckt in
  beiden Layouts `${masked:inner=${message}}` — der `MaskingLayoutRenderer` ersetzt
  JSON-Secret-Felder, Connection-String-Passwörter und Bearer-Token. Er registriert sich über
  einen `[ModuleInitializer]`, damit er auch im Testprozess (kein `Main`) vor dem ersten Logger
  steht. **Nicht** über eine `<variable>` einbinden: ob NLog einen Variablenwert erneut als
  Layout interpretiert, ist nicht garantiert, und die Maskierung fiele still aus.
- **Globaler Exception-Handler**: Desktop hakt in `AppDomain.CurrentDomain.UnhandledException` +
  `TaskScheduler.UnobservedTaskException` und ruft `LogService.Fatal`. Server-Middleware setzt
  RFC-7807-ProblemDetails.

---

## Definition of Done (Kurz)

- [ ] Tests vorhanden, `dotnet test` grün (Client **und** Server)
- [ ] Bei Schema-Änderung: EF-Migration erzeugt; Live-DB-Migrate beim Redeploy bedacht
- [ ] Server-Modus deckt die Fläche vollständig ab (kein stiller lokaler Fallback)
- [ ] Privatsphäre-Maskierung bleibt in Plan/PDF/Mail erhalten; Finalisierungs-Sicht-Regel
      respektiert (Server + clientseitiges Impersonate-Nachfiltern)
- [ ] Keine Secrets im Log/Repo; NLog-Ausgabe nach Änderung geprüft
- [ ] Alle Fenster über `ChromeWindow`, resizable; InfoBox mit BMC-Button und Update-Prüfung
- [ ] Keine Farbliterale im XAML; neue Farben als Rollen-Key in `Styles/Palette.axaml`
- [ ] Neue Paketversion nur in `Directory.Packages.props`; nach Avalonia-Bump Skia-Linie geprüft
- [ ] README (Nutzersicht) und CLAUDE.md (Entwicklersicht) im selben Commit mitgezogen
- [ ] Nach Tag+Push: `gh run list` prüfen; Failures sofort im nächsten Tag reparieren
