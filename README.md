# FlexFamily Calendar

Familienplaner für Arbeitszeiten (Eltern, Angestellte, Au-Pairs) und Aktivitäten (Kinder,
Au-Pairs, Eltern). Avalonia 12 / .NET 10, single codebase für Desktop (Linux/Win/macOS)
und Web (Avalonia.Browser / WASM).

## Modi

Die App läuft entweder **lokal** (Desktop, JSON-Dateien) oder **gegen einen Server**
(Desktop oder Browser, alles über `/api/*`). Kein Parallelbetrieb, kein Sync — bewusst
genau ein Backend zur Laufzeit.

- **Lokal:** Settings + Plan-Daten in `~/.local/share/FlexFamilyCalendar/` (Linux) bzw.
  `%LOCALAPPDATA%\FlexFamilyCalendar\` (Windows). SMTP / KI-Keys per Admin-UI in
  `settings.json`, Passwörter mit lokalem AES-Key verschlüsselt.
- **Server:** Postgres + ASP.NET-Core-API hinter Caddy (HTTPS via Let's Encrypt).
  Browser-Head wird von Caddy direkt mit der API zusammen ausgeliefert (gleicher Origin,
  kein CORS). SMTP und KI-API-Schlüssel liegen serverseitig in **ENV-Variablen**.

Live: <https://flexfamily.cloud>

## Server-Deployment (Docker Compose)

```bash
cd server
cp .env.example .env   # falls vorhanden, sonst Werte direkt setzen
docker compose up -d --build
```

`server/docker-compose.yml` erwartet folgende ENV-Variablen (alle optional außer
`JWT_KEY` und dem Erst-Admin):

### Pflicht beim allerersten Start

| Variable | Zweck |
|---|---|
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

## Build (lokal)

```bash
# Desktop
dotnet run --project src/FlexFamilyCalendar.csproj

# Browser/WASM (publish ins AppBundle, dann mit beliebigem HTTP-Server ausliefern)
dotnet publish browser/FlexFamilyCalendar.Browser.csproj -c Release -o /tmp/spa

# Tests
dotnet test tests/FlexFamilyCalendar.Tests
dotnet test tests/FlexFamilyCalendar.Api.Tests
```

Anforderungen: .NET 10 SDK, für den WASM-Build zusätzlich `dotnet workload install
wasm-tools` und auf manchen Distros `python3` (Emscripten ruft `python` auf).

## Releases

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
