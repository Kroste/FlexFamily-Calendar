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
- **JSON:** Ausschließlich `System.Text.Json` mit gemeinsamem `JsonOptions.Pretty`
  (PropertyNamingPolicy=null, damit alte PascalCase-JSON-Files weiter lesbar bleiben);
  Newtonsoft.Json ist als Dependency raus.
- **Datenschutz:** Fremde Krank-/Urlaubsgründe erscheinen nur als „Abwesend" (Maskierung pro Betrachter),
  im Plan, PDF und Mail-Versand (je Empfänger aus dessen Sicht).
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
  **Über die API alles loggen** (Methode/Pfad/Status). Logs nach Änderungen ansehen (Teil der DoD).
- **Passwörter/Tokens/Secrets NIEMALS loggen** und nie im Klartext ablegen/committen
  (Desktop: `SecretService` mit AES-Keyfile, Browser: Origin-Isolation, Android: Isolated Storage
  über `SecretService`; API-Keys serverseitig in ENV).
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
- [ ] Alle Fenster über `ChromeWindow`, resizable; InfoBox mit BMC-Button
- [ ] Nach Tag+Push: `gh run list` prüfen; Failures sofort im nächsten Tag reparieren
