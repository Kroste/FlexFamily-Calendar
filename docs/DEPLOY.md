# Betrieb — FlexFamily Calendar

Dieses Dokument richtet sich an Operatoren, die FlexFamily Calendar selbst auf
einem Server betreiben. Für Endanwender-Doku siehe die [README.md](../README.md).

## Architektur

- **API** — ASP.NET Core Minimal API, EF Core, JWT-Auth, BCrypt-Passwörter.
- **DB** — PostgreSQL 17. Startup-Migrate.
- **Reverse Proxy** — Caddy mit automatischem HTTPS (Let's Encrypt) und
  eingebetteter WASM-SPA. Single-Origin: `/api/*` und `/health` gehen an die
  API, alles andere ist die SPA.
- **Watchtower** — pollt Docker Hub alle 24 h und aktualisiert die
  API- und Caddy-Container automatisch, wenn ein neues `:latest`-Image
  bereitsteht.

Alle Komponenten laufen als Docker-Container, orchestriert von einer einzigen
`docker-compose.yml`.

## Voraussetzungen auf dem Server

- Debian 12/13 (getestet) oder eine andere aktuelle Linux-Distribution
- Docker Engine 25+ und Docker Compose v2
- Ein A-/AAAA-Record deiner Domain (`flexfamily.beispiel.de`) auf die
  Server-IP — Caddy braucht ihn für die Let's-Encrypt-Challenge
- Ports **80** und **443** offen von außen (für ACME und HTTPS)

## Erst-Setup

```bash
mkdir -p ~/flexfamily && cd ~/flexfamily

# docker-compose.yml aus dem gewünschten Release-Tag ziehen
curl -fsSL https://raw.githubusercontent.com/Kroste/FlexFamily-Calendar/latest/server/docker-compose.yml \
  -o docker-compose.yml
```

### `.env` mit Secrets

```bash
cat > ~/flexfamily/.env <<EOF
# Postgres — bleibt intern im Docker-Netz, trotzdem zufällig.
DB_PASSWORD=$(openssl rand -base64 24 | tr -d '=+/')

# JWT-Signing — MUSS stabil bleiben, sonst kicken bestehende Tokens raus.
JWT_KEY=$(openssl rand -base64 64 | tr -d '=+/' | head -c 96)

# Erst-Admin (nur beim allerersten Start relevant, danach aus DB).
ADMIN_USER=admin
ADMIN_PASSWORD=$(openssl rand -base64 12 | tr -d '=+/')

# Watchtower-Pollingintervall (Sekunden, default 24h).
WATCHTOWER_INTERVAL=86400

# SMTP + AI später nachtragen, leer lassen ist ok.
EOF
chmod 600 ~/flexfamily/.env
```

**Wichtig:** merk dir das Admin-Passwort und ändere es nach dem ersten Login im
UI. Danach kannst du `ADMIN_USER`/`ADMIN_PASSWORD` aus der `.env` löschen —
`Seed__Admin*` wird nur bei leerer DB verwendet.

### Domain in der Caddyfile

Die Caddyfile ist ins Caddy-Image gebacken und fest auf `flexfamily.cloud`
verdrahtet. Für eine andere Domain: `server/Caddyfile` im Repo forken und
anpassen, dann eigenes Image bauen (`server/Caddy.Dockerfile`, siehe unten).

### Starten

```bash
docker compose pull
docker compose up -d

# Sanity
sleep 20
docker compose ps
docker compose logs --tail=30 api | grep -E "migration|Seed|Admin"
docker compose logs --tail=20 caddy | grep -E "certificate|obtained|serving"
curl -sS https://deine-domain.de/health   # {"status":"ok",...}
```

## ENV-Variablen (Referenz)

### Pflicht

| Variable | Zweck |
|---|---|
| `DB_PASSWORD` | Postgres-Passwort |
| `JWT_KEY` | JWT-Signaturschlüssel, mind. 32 Bytes |
| `ADMIN_USER` / `ADMIN_PASSWORD` | Erst-Admin (nur bei leerer DB) |

### Docker-Image-Auswahl

| Variable | Default | Zweck |
|---|---|---|
| `DOCKERHUB_USER` | `larsoste` | Docker-Hub-Account (offizielle Images) |
| `IMAGE_TAG` | `latest` | Image-Tag (Watchtower folgt `:latest`) |
| `WATCHTOWER_INTERVAL` | `86400` | Sekunden zwischen Update-Checks |

### SMTP (optional — `/api/mail/send-week-plan`)

Wenn nicht gesetzt, antwortet der Endpunkt mit „SMTP serverseitig nicht
konfiguriert". Der Mail-Button in der App bleibt sichtbar, meldet aber einen
Warn-Log.

| Variable | Default | Zweck |
|---|---|---|
| `SMTP_HOST` | _(leer)_ | z.B. `smtp.hostinger.com` |
| `SMTP_PORT` | `587` | |
| `SMTP_FROM` | _(leer)_ | Absender-Adresse |
| `SMTP_USER` | _(leer)_ | Login |
| `SMTP_PASSWORD` | _(leer)_ | Login-Passwort |
| `SMTP_USE_SSL` | `true` | STARTTLS / SSL |

### KI-Provider (optional — `/api/ai/complete`)

| Variable | Provider |
|---|---|
| `AI_ANTHROPIC_KEY` | Anthropic (Claude) |
| `AI_OPENAI_KEY` | OpenAI / ChatGPT |
| `AI_GEMINI_KEY` | Google Gemini |
| `AI_PERPLEXITY_KEY` | Perplexity |

Nicht gesetzte Provider antworten mit „Schlüssel serverseitig nicht gesetzt".
Der Client zeigt sie trotzdem im KI-Tab, aber ein Aufruf schlägt kontrolliert
fehl.

## Update-Betrieb

Watchtower pollt Docker Hub im konfigurierten Intervall. Sobald das
Release-Workflow im GitHub-Repo einen neuen Tag baut, sind API + Caddy nach
höchstens `WATCHTOWER_INTERVAL` Sekunden aktualisiert.

Manuell aktualisieren:
```bash
cd ~/flexfamily
docker compose pull
docker compose up -d
```

Bei Client-seitigen Änderungen (SPA-Assets im Caddy-Image) reicht:
```bash
docker compose pull caddy
docker compose up -d caddy
```
Browser danach mit **Hard-Refresh** (`Ctrl+Shift+R`) oder DevTools →
Application → Clear site data neuladen.

## Backup

Postgres-Volume regelmäßig sichern:

```bash
docker exec flexfamily-db-1 pg_dumpall -U flexfamily > backup-$(date +%F).sql
```

Restore auf eine leere DB:

```bash
docker compose up -d db
sleep 10
docker exec -i flexfamily-db-1 psql -U flexfamily -d postgres < backup-YYYY-MM-DD.sql
docker compose up -d api caddy
```

## Docker-Images selbst bauen

Falls du eine eigene Domain, eigenes Branding oder eigene Caddyfile hast:

```bash
git clone https://github.com/Kroste/FlexFamily-Calendar.git
cd FlexFamily-Calendar

docker build -t meineorg/flexfamily-calendar-api:latest \
  -f server/FlexFamilyCalendar.Api/Dockerfile .

docker build -t meineorg/flexfamily-calendar-caddy:latest \
  -f server/Caddy.Dockerfile .
```

Dann in der `.env` `DOCKERHUB_USER=meineorg` setzen. Bei eigener Caddyfile
`server/Caddyfile` vor dem Build anpassen (steckt fest im Image).

## Troubleshooting

### Caddy zieht kein Zertifikat
- DNS-A-Record zeigt auf den Server?
- Ports 80 + 443 offen von außen?
- `docker compose logs caddy` → dort steht die genaue Meldung von Let's Encrypt.

### API bricht mit „Connection refused"
- DB-Container läuft? `docker compose ps db`
- `DB_PASSWORD` in `.env` passt? Bei kopiertem Volume aus altem Deploy: das
  interne Postgres-Passwort ist das ALTE — entweder das alte in `.env`
  eintragen oder mit `ALTER USER flexfamily WITH PASSWORD '...'` setzen.

### Watchtower crashloopt
- `client version too old` → in `docker-compose.yml` ist
  `DOCKER_API_VERSION: "1.44"` gesetzt (seit v0.1.8).

### SRI-Fehler im Browser nach Update
Der Browser hat noch altes `dotnet.boot.js` im Cache mit falschen Hashes.
Devtools → Application → Storage → **Clear site data**, dann reload. Ab
v0.1.16 blockt Caddy dieses Fenster über erweiterte `no-cache`-Header
präventiv.

## Weitere Referenzen

- Projekt-Kanon: [CLAUDE.md](../CLAUDE.md)
- Avalonia-12-Fallstricke: [avalonia12-canvas-gotchas.md](avalonia12-canvas-gotchas.md)
