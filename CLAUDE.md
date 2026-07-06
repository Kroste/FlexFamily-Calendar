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

---

## Projekt

- **Name:** `FlexFamily Calendar`
- **Kurzbeschreibung:** Familienplaner für Arbeitszeiten, Schichten, Aktivitäten (Schule/Kita/Sport),
  Krankmeldungen, Urlaubswünsche und Schichttausch — für Eltern, Kinder, Angestellte und Au-Pairs.
- **Repository:** `https://github.com/Kroste/FlexFamily-Calendar`
- **Lokaler Pfad:** `/home/OsteL/Entwicklung/FlexFamily Calendar`
- **Live:** `https://flexfamily.cloud` (Hostinger-VPS, Debian 13, Docker Compose:
  Postgres 17 + ASP.NET-Core-API + Caddy-Reverse-Proxy mit HTTPS/Let's Encrypt).

### Projektspezifische Besonderheiten

- **Ein Codebase, mehrere Heads:** Avalonia 12 / .NET 10 → **Desktop** (`src/`, Linux/Windows) und
  **Browser/WASM** (`browser/`). Geteilte UI/Domänenlogik. **Android-Head** ist geplant, aber
  ans Ende geschoben.
- **Speicher-Modus = entweder/oder, kein Parallelbetrieb, kein Sync:** Die App läuft **entweder**
  lokal gegen JSON-Dateien (`StorageService`) **oder** gegen die Server-API (`ApiStorageService`).
  `IStorageService` ist die Naht. `AppSettings.UseServer` + `ServerUrl` (lokale Installations-Config,
  **keine** Domänendaten) schalten um.
  - **Im Server-Modus MUSS alles über die API laufen — kein lokaler Fallback.** Fehlende Server-Flächen
    werden autonom als Endpunkte nachgezogen, nicht aus lokalen Dateien bedient.
- **Server = Single Source of Truth + Sicherheitsgrenze:** Authn/Authz und Privatsphäre-Maskierung
  werden **serverseitig** erzwungen (Client nie vertrauen). JWT-Login, Rollen (Admin/User) und
  Kategorien (Parent/Child/Employee/AuPair).
- **KI ist additiv, nie im kritischen Pfad** — die App funktioniert ohne KI voll. Im Server-Modus
  liegt der API-Key serverseitig in ENV (`ApiAiProvider`), lokal über die Einstellungen.
- **PDF-Export:** eigener abhängigkeitsfreier PDF-Writer (reines Managed). **Keine native
  PDF-/Skia-Lib hinzufügen** — QuestPDF kollidiert in-process mit Avalonias SkiaSharp.
- **Datenschutz:** Fremde Krank-/Urlaubsgründe erscheinen nur als „Abwesend" (Maskierung pro Betrachter),
  im Plan, PDF und Mail-Versand (je Empfänger aus dessen Sicht).

---

## Repo-Struktur

```
src/       Avalonia-Desktop-Head (UI, ViewModels, Services, Models) — geteilte Logik
browser/   Avalonia-Browser/WASM-Head
server/    FlexFamilyCalendar.Api (ASP.NET Core Minimal API, EF Core, Postgres)
tests/     FlexFamilyCalendar.Tests (Client) + FlexFamilyCalendar.Api.Tests (Server) — xUnit
docs/      Screenshots, Logo
```

- **Server-DB-Schema** ändert sich über **EF-Migrationen**
  (`server/FlexFamilyCalendar.Api/Migrations/`). Nach Feldänderungen:
  `dotnet ef migrations add <Name>` — und **Live-DB beim Redeploy migrieren**
  (`dotnet ef database update` bzw. Startup-Migrate), sonst driften Code und Schema.
- **Übersetzung Client↔Server** liegt in `src/Services/Api/*Mapping.cs` (User, Entry, ActivityType,
  RecurringActivity, ShiftSwap, Notification). Eintrags-Modell-Unterschied: Desktop = Abwesenheit als
  Tag-pro-Eintrag mit `AbsenceGroupId`, Server = ein Bereich-Eintrag (Date+EndDate).

---

## Tech-Stack (Baseline)

- **.NET 10** / **C#** (LangVersion `latest`, `ImplicitUsings`, `Nullable enable`, `TreatWarningsAsErrors`)
- **Avalonia ≥ 12.0.4**, MVVM via **CommunityToolkit.Mvvm**
- DI/Hosting via **Microsoft.Extensions.DependencyInjection**, Logging via **NLog**
- Server: **ASP.NET Core Minimal API**, **EF Core** + **Npgsql/PostgreSQL**, JWT-Auth, BCrypt-Passwörter
- Versionierung via **MinVer** (Git-Tag `vX.Y.Z`), GitHub-Account **Kroste** (`lars-oste@gmx.de`)

---

## Logging & Secrets

- **Grundsätzlich alles loggen** (Trace/Debug für Abläufe, Info für Aktionen, Warn/Error für Probleme).
  **Über die API alles loggen** (Methode/Pfad/Status). Logs nach Änderungen ansehen (Teil der DoD).
- **Passwörter/Tokens/Secrets NIEMALS loggen** und nie im Klartext ablegen/committen
  (Desktop: DPAPI/SecretService, Browser: Origin-Isolation; API-Keys serverseitig in ENV).

---

## Definition of Done (Kurz)

- [ ] Tests vorhanden, `dotnet test` grün (Client **und** Server)
- [ ] Bei Schema-Änderung: EF-Migration erzeugt; Live-DB-Migrate beim Redeploy bedacht
- [ ] Server-Modus deckt die Fläche vollständig ab (kein stiller lokaler Fallback)
- [ ] Privatsphäre-Maskierung bleibt in Plan/PDF/Mail erhalten
- [ ] Keine Secrets im Log/Repo; NLog-Ausgabe nach Änderung geprüft
- [ ] Alle Fenster über `ChromeWindow`, resizable; InfoBox mit BMC-Button
