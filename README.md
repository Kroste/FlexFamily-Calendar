<div align="center">

<!-- Logo: Bitte ein PNG (256×256 oder größer) als docs/logo.png ablegen. -->
<img src="docs/logo.png" alt="FlexFamily Calendar" width="160" />

# FlexFamily Calendar

Familienplaner für Arbeitszeiten, Schichten, Schule, Kita, Sport, Krankmeldungen,
Urlaubswünsche, Schichttausch — für Eltern, Kinder, Angestellte und Au-Pairs.

Ein Codebase (Avalonia 12 / .NET 10) → Desktop (Linux, Windows) und Web (WASM).

</div>

---

## Inhalt

- [Was die App kann](#was-die-app-kann)
- [Screenshots](#screenshots)
- [Download & Installation](#download--installation)
- [Auto-Update](#auto-update)
- [Modi: lokal vs. Server](#modi-lokal-vs-server)
- [Server-Deployment](#server-deployment-docker-compose)
- [Build & Tests](#build--tests)
- [Releases & CI](#releases--ci)

---

## Was die App kann

- **Tabellarische Plansicht** Person × Wochentag — Eltern, Kinder, Angestellte, Au-Pairs in einer Sicht.
- **Schichten, Aktivitäten, Übernachtungen, Abwesenheiten** (Urlaub/Krank/Abwesend als Datumsbereich).
- **Drag & Drop** einer Schicht auf einen anderen Tag oder eine andere Person → Verschieben/Kopieren.
- **Konfigurierbare Aktivitäts-Kategorien** je Rolle (z.B. Sprachkurs nur für Au-Pair, Sport nur für Kinder).
- **Wiederkehrende Aktivitäten** als Wochen-Regel (Wochentag + Zeit + Kategorie), pro Regel „an Feiertagen ausfallen" wählbar.
- **Feiertage** je Bundesland im Plan, im PDF, in der Logik.
- **Stundenkonto, Tages-/Wochenlimit, Ruhezeit, Doppelbelegung** als Regeln; Übernachtungen mit pauschaler Gutschrift.
- **Krankmeldung-Selbst** + Admin-Umplanung mit KI-Vorschlag.
- **Schichttausch-Workflow** (Vorschlag → Bestätigung, eingehende/ausgehende Anfragen).
- **PDF-Wochenexport** und **Mail-Versand** (jeder Empfänger bekommt sein aus seiner Sicht maskiertes PDF — Privatsphäre bleibt).
- **KI-Unterstützung** mit Anthropic, OpenAI, Gemini, Perplexity (Cloud) oder lokalem Llama.
- **Privatsphäre-Maskierung**: Fremde sehen Krank/Urlaub nur als „Abwesend" ohne Grund.
- **Mehrsprachig** (DE/EN), Sprache und Theme pro Benutzer.
- **Multi-User / Multi-Device**: Browser & Desktop syncen alle 30 s gegen denselben Server.

## Screenshots

<!--
  Screenshots werden vom Repo-Owner bereitgestellt. Erwartete Dateien:
  - docs/screenshots/calendar.png       (Hauptplan)
  - docs/screenshots/entry-editor.png   (Eintrag-Editor)
  - docs/screenshots/admin-tabs.png     (Admin → Tabs)
  - docs/screenshots/hours-account.png  (Stundenkonto)
  - docs/screenshots/month-overview.png (Monatsübersicht)
-->

| Plansicht | Eintrag-Editor |
|---|---|
| ![Plansicht](docs/screenshots/calendar.png) | ![Eintrag-Editor](docs/screenshots/entry-editor.png) |

| Admin-Bereich | Stundenkonto |
|---|---|
| ![Admin-Bereich](docs/screenshots/admin-tabs.png) | ![Stundenkonto](docs/screenshots/hours-account.png) |

## Download & Installation

Auf jeder [Release-Seite](https://github.com/Kroste/FlexFamily-Calendar/releases/latest)
liegen drei Desktop-Pakete. Die Binaries sind **self-contained**, .NET muss nicht
installiert sein.

### Linux

**AppImage** (eine Datei, doppelklickbar, kein Install):

```bash
curl -L -o FlexFamilyCalendar.AppImage \
  https://github.com/Kroste/FlexFamily-Calendar/releases/latest/download/FlexFamilyCalendar-vX.Y.Z-x86_64.AppImage
chmod +x FlexFamilyCalendar.AppImage
./FlexFamilyCalendar.AppImage
```

**Tar.gz** (extrahiert ins Wunsch-Verzeichnis):

```bash
curl -L -o ffc.tar.gz \
  https://github.com/Kroste/FlexFamily-Calendar/releases/latest/download/FlexFamilyCalendar-vX.Y.Z-linux-x64.tar.gz
mkdir -p ~/apps/FlexFamilyCalendar && tar -xzf ffc.tar.gz -C ~/apps/FlexFamilyCalendar
~/apps/FlexFamilyCalendar/FlexFamilyCalendar.Desktop
```

### Windows

ZIP-Datei aus der Release-Seite herunterladen, irgendwohin entpacken,
`FlexFamilyCalendar.Desktop.exe` per Doppelklick starten.

## Auto-Update

Die Desktop-App prüft beim Start (und in einem konfigurierbaren Intervall, Default
24 h) gegen die GitHub-Releases-API, ob eine neuere Version vorliegt.

- Verfügbares Update → Dialog mit Changelog. Buttons:
  - **Jetzt installieren** — App lädt das passende Asset (AppImage / tar.gz / zip),
    startet einen Helper-Prozess, der die laufende Installation ersetzt, und startet
    die neue Version automatisch.
  - **Release-Seite** — öffnet die GitHub-Release-Seite im Browser für manuellen Download.
  - **Später** — Frage in 24 h erneut.
  - **Überspringen** — diese Version nicht mehr anbieten.
- Steuerung in **Admin → Einstellungen → Updates**: ein/aus, Intervall, manueller
  „Jetzt prüfen"-Button.

Der **Browser-Head** hat kein Auto-Update — er wird vom Server frisch ausgeliefert
(siehe Watchtower unten).

## Modi: lokal vs. Server

Die App läuft entweder **lokal** (Desktop, JSON-Dateien) oder **gegen einen Server**
(Desktop oder Browser, alles über `/api/*`). Kein Parallelbetrieb, kein Sync — bewusst
genau ein Backend zur Laufzeit.

- **Lokal:** Settings + Plan-Daten in `~/.local/share/FlexFamilyCalendar/` (Linux) bzw.
  `%LOCALAPPDATA%\FlexFamilyCalendar\` (Windows). SMTP / KI-Keys per Admin-UI in
  `settings.json`, Passwörter mit lokalem AES-Key verschlüsselt.
- **Server:** Postgres + ASP.NET-Core-API hinter Caddy (HTTPS via Let's Encrypt).
  Browser-Head wird von Caddy direkt mit der API zusammen ausgeliefert (gleicher Origin,
  kein CORS). SMTP- und KI-API-Schlüssel liegen serverseitig in **ENV-Variablen**.
  Watchtower aktualisiert API + Caddy automatisch alle 24 h.

> Eine Live-Instanz läuft (privat, nicht öffentlich freigegeben) unter
> `flexfamily.cloud` — kein Demo-Zugang.

## Server-Deployment (Docker Compose)

Production zieht die fertigen Images vom Release-Workflow aus Docker Hub —
Watchtower aktualisiert sie alle 24 h automatisch.

```bash
cd server
cp .env.example .env   # falls vorhanden, sonst Werte direkt setzen
docker compose pull && docker compose up -d
```

Für lokale Entwicklung statt Hub-Pull: die `build:`-Blöcke in `docker-compose.yml`
einkommentieren und die `image:`-Zeilen entsprechend auskommentieren, dann
`docker compose up -d --build`.

`server/docker-compose.yml` erwartet folgende ENV-Variablen (alle optional außer
`JWT_KEY` und dem Erst-Admin):

### Pflicht beim allerersten Start

| Variable | Zweck |
|---|---|
| `DOCKERHUB_USER` | Docker-Hub-Account, von dem `flexfamily-calendar-api` / `-caddy` gezogen werden. Default `kroste`. |
| `IMAGE_TAG` | Image-Tag, z.B. `v1.0.0` oder `latest`. Default `latest` (Watchtower aktualisiert dann automatisch). |
| `WATCHTOWER_INTERVAL` | Sekunden zwischen Image-Checks. Default `86400` (24 h). |
| `DB_PASSWORD` | Postgres-Passwort. Default `changeme` — bitte ändern. |
| `JWT_KEY` | JWT-Signaturschlüssel, mind. 32 Bytes. Default ist eine sichtbare Warnung — bitte ändern. |
| `ADMIN_USER` / `ADMIN_PASSWORD` | Erst-Admin. Wird nur angelegt, wenn die DB leer ist (idempotent). |

### Optional — Mail-Versand (`/api/mail/send-week-plan`)

Wenn nicht gesetzt, antwortet der Endpunkt mit „SMTP serverseitig nicht konfiguriert"
und der Mail-Button im Browser zeigt einen Warn-Log; sonst kein Crash.

| Variable | Default | Zweck |
|---|---|---|
| `SMTP_HOST` | _(leer)_ | SMTP-Server, z.B. `smtp.hostinger.com` |
| `SMTP_PORT` | `587` | |
| `SMTP_FROM` | _(leer)_ | Absender-Adresse |
| `SMTP_USER` | _(leer)_ | Login |
| `SMTP_PASSWORD` | _(leer)_ | Login-Passwort |
| `SMTP_USE_SSL` | `true` | STARTTLS / SSL |

### Optional — KI-Provider (`/api/ai/complete`)

Jeder Provider hat seine eigene Variable; nicht gesetzte Provider antworten mit
„Schlüssel serverseitig nicht gesetzt".

| Variable | Provider |
|---|---|
| `AI_ANTHROPIC_KEY` | Anthropic (Claude) |
| `AI_OPENAI_KEY` | OpenAI / ChatGPT |
| `AI_GEMINI_KEY` | Google Gemini |
| `AI_PERPLEXITY_KEY` | Perplexity |

Im Browser-Modus zeigt der Admin → KI-Tab daher **nur** Provider-Auswahl + optionales
Modell — kein API-Schlüssel-Feld. Die Settings-UI für SMTP ist im Browser-Modus
ausgeblendet, weil die Werte nicht benutzt werden (Server entscheidet).

## Build & Tests

```bash
# Desktop (lokal starten)
dotnet run --project desktop/FlexFamilyCalendar.Desktop.csproj

# Browser/WASM (publish ins AppBundle, dann mit beliebigem HTTP-Server ausliefern)
dotnet publish browser/FlexFamilyCalendar.Browser.csproj -c Release -o /tmp/spa

# Tests
dotnet test tests/FlexFamilyCalendar.Tests
dotnet test tests/FlexFamilyCalendar.Api.Tests
```

Anforderungen: .NET 10 SDK, für den WASM-Build zusätzlich `dotnet workload install
wasm-tools` und auf manchen Distros `python3` (Emscripten ruft `python` auf).

## Releases & CI

Auf jedes Tag `vX.Y.Z` (z.B. via `gh release create v1.0.0 --generate-notes`)
läuft `.github/workflows/release.yml` und baut fünf Artefakte parallel:

| Artefakt | Wo |
|---|---|
| `FlexFamilyCalendar-vX.Y.Z-linux-x64.tar.gz` | GitHub-Release-Asset |
| `FlexFamilyCalendar-vX.Y.Z-win-x64.zip` | GitHub-Release-Asset |
| `FlexFamilyCalendar-vX.Y.Z-x86_64.AppImage` | GitHub-Release-Asset |
| `<docker-user>/flexfamily-calendar-api:vX.Y.Z` + `:latest` | Docker Hub |
| `<docker-user>/flexfamily-calendar-caddy:vX.Y.Z` + `:latest` | Docker Hub |

Die Desktop-Pakete sind self-contained Single-File-Binaries — kein installiertes
.NET nötig.

Damit der Workflow zu Docker Hub pushen kann, müssen in GitHub unter
**Settings → Secrets and variables → Actions** zwei Secrets stehen:

- `DOCKERHUB_USERNAME` — dein Docker-Hub-Username
- `DOCKERHUB_TOKEN` — ein Personal Access Token (Docker Hub → Account Settings
  → Security → New Access Token), kein Passwort

Manuell auslösen lässt sich der Workflow über GitHub → Actions → Release →
„Run workflow" mit einem Test-Tag-Namen.
