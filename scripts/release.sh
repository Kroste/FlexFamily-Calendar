#!/usr/bin/env bash
#
# Release-Skript: prüft den Git-Zustand, fragt den Versions-Bump ab, setzt einen
# annotierten Tag vX.Y.Z und pusht ihn. Der Tag triggert .github/workflows/release.yml
# (Desktop win/linux, AppImage, Android-APK, Docker-Images).
#
# Die Version kommt über MinVer aus dem Tag — es gibt kein <Version> in einer csproj,
# das mitgezogen werden müsste.

set -euo pipefail

cd "$(dirname "$0")/.."

# --- Vorbedingungen ---------------------------------------------------------

branch="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$branch" != "main" ]]; then
    echo "Abbruch: Releases werden von main gebaut, aktuell steht HEAD auf '$branch'." >&2
    exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
    echo "Abbruch: Arbeitsverzeichnis ist nicht sauber. Erst committen." >&2
    git status --short >&2
    exit 1
fi

git fetch --quiet origin main
if [[ -n "$(git log origin/main..HEAD --oneline)" ]]; then
    echo "Abbruch: Es gibt lokale Commits, die noch nicht gepusht sind." >&2
    git log origin/main..HEAD --oneline >&2
    exit 1
fi

# --- Aktuelle Version ermitteln ---------------------------------------------

last_tag="$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || echo 'v0.0.0')"
version="${last_tag#v}"
IFS='.' read -r major minor patch <<< "$version"

echo "Letzter Tag: $last_tag"
echo
echo "  1) Patch  → v$major.$minor.$((patch + 1))"
echo "  2) Minor  → v$major.$((minor + 1)).0"
echo "  3) Major  → v$((major + 1)).0.0"
echo
read -rp "Bump [1/2/3, Enter = 1]: " choice

case "${choice:-1}" in
    1|"") new="v$major.$minor.$((patch + 1))" ;;
    2)    new="v$major.$((minor + 1)).0" ;;
    3)    new="v$((major + 1)).0.0" ;;
    *)    echo "Ungültige Auswahl." >&2; exit 1 ;;
esac

# --- Tests, bevor getaggt wird ----------------------------------------------

echo
echo "Tests laufen (Release)…"
dotnet test FlexFamilyCalendar.slnx -c Release

# --- Tag setzen und pushen --------------------------------------------------

echo
read -rp "Tag $new setzen und pushen? [j/N]: " confirm
if [[ "${confirm,,}" != "j" ]]; then
    echo "Abgebrochen — nichts geändert."
    exit 0
fi

git tag -a "$new" -m "Release $new"
git push origin "$new"

echo
echo "Tag $new gepusht. Release-Workflow läuft:"
echo "  gh run list --repo Kroste/FlexFamily-Calendar --limit 3"
