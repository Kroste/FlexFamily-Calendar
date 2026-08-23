<#
.SYNOPSIS
    Release-Skript für Windows: prüft den Git-Zustand, fragt den Versions-Bump ab,
    setzt einen annotierten Tag vX.Y.Z und pusht ihn.

.DESCRIPTION
    Pendant zu scripts/release.sh. Der Tag triggert .github/workflows/release.yml
    (Desktop win/linux, AppImage, Android-APK, Docker-Images). Die Version kommt über
    MinVer aus dem Tag — es gibt kein <Version> in einer csproj, das mitzuziehen wäre.

    Die Datei ist UTF-8 mit BOM gespeichert: Windows PowerShell 5.1 liest .ps1 ohne BOM
    als ANSI und macht aus "ü" ein "³".
#>

$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

# --- Vorbedingungen ---------------------------------------------------------

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'main') {
    Write-Error "Abbruch: Releases werden von main gebaut, aktuell steht HEAD auf '$branch'."
}

if ((git status --porcelain)) {
    git status --short
    Write-Error 'Abbruch: Arbeitsverzeichnis ist nicht sauber. Erst committen.'
}

git fetch --quiet origin main
$unpushed = git log origin/main..HEAD --oneline
if ($unpushed) {
    $unpushed
    Write-Error 'Abbruch: Es gibt lokale Commits, die noch nicht gepusht sind.'
}

# --- Aktuelle Version ermitteln ---------------------------------------------

# NICHT `git describe --abbrev=0` verwenden: das liefert den nach Commit-Distanz
# NÄCHSTGELEGENEN erreichbaren Tag, nicht den höchsten. In diesem Repo kommt dabei
# v0.1.45 heraus, obwohl längst v0.11.x aktuell ist — der nächste Release wäre
# hinter dem Stand zurückgefallen. `--sort=-v:refname` sortiert semantisch korrekt
# (v0.11.2 > v0.1.45), `--merged HEAD` beschränkt auf erreichbare Tags.
$lastTag = (git tag --list 'v*' --merged HEAD --sort=-v:refname | Select-Object -First 1)
if (-not $lastTag) { $lastTag = 'v0.0.0' }

$parts = $lastTag.TrimStart('v').Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

Write-Host "Letzter Tag: $lastTag"
Write-Host ''
Write-Host ("  1) Patch  -> v{0}.{1}.{2}" -f $major, $minor, ($patch + 1))
Write-Host ("  2) Minor  -> v{0}.{1}.0"   -f $major, ($minor + 1))
Write-Host ("  3) Major  -> v{0}.0.0"     -f ($major + 1))
Write-Host ''
$choice = Read-Host 'Bump [1/2/3, Enter = 1]'

switch ($choice) {
    ''      { $new = "v$major.$minor.$($patch + 1)" }
    '1'     { $new = "v$major.$minor.$($patch + 1)" }
    '2'     { $new = "v$major.$($minor + 1).0" }
    '3'     { $new = "v$($major + 1).0.0" }
    default { Write-Error 'Ungültige Auswahl.' }
}

# --- Tests, bevor getaggt wird ----------------------------------------------

Write-Host ''
Write-Host 'Tests laufen (Release)…'
dotnet test FlexFamilyCalendar.slnx -c Release
if ($LASTEXITCODE -ne 0) { Write-Error 'Tests fehlgeschlagen — kein Tag gesetzt.' }

# --- Tag setzen und pushen --------------------------------------------------

Write-Host ''
$confirm = Read-Host "Tag $new setzen und pushen? [j/N]"
if ($confirm.ToLower() -ne 'j') {
    Write-Host 'Abgebrochen — nichts geändert.'
    exit 0
}

git tag -a $new -m "Release $new"
git push origin $new

Write-Host ''
Write-Host "Tag $new gepusht. Release-Workflow läuft:"
Write-Host '  gh run list --repo Kroste/FlexFamily-Calendar --limit 3'
