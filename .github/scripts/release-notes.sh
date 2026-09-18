#!/usr/bin/env bash
# Gibt den Abschnitt `## <tag> …` aus CHANGELOG.md aus (ohne die Überschrift selbst).
# Exitcode 1, wenn es keinen Abschnitt oder nur einen leeren gibt — der Release-Workflow bricht
# dann ab, statt ein Release ohne Text anzulegen. Der Text erscheint in der App im Update-Dialog.
#
#   .github/scripts/release-notes.sh v0.20.0 [CHANGELOG.md]
set -euo pipefail
tag="${1:?Tag fehlt, z.B. v0.20.0}"
file="${2:-CHANGELOG.md}"

notes="$(awk -v tag="$tag" '
  /^## / {
    if (inside) exit
    # Überschrift ist "## vX.Y.Z" oder "## vX.Y.Z — Datum": das erste Wort muss exakt passen,
    # sonst träfe v0.2.0 auch den Abschnitt von v0.20.0.
    split($0, parts, " ")
    if (parts[2] == tag) { inside = 1; next }
  }
  inside { print }
' "$file")"

# Leerzeilen am Anfang und Ende weg
notes="$(printf '%s\n' "$notes" | sed -e '/./,$!d' | sed -e ':a' -e '/^\n*$/{$d;N;ba' -e '}')"

if [ -z "${notes//[[:space:]]/}" ]; then
  echo "Kein Abschnitt '## $tag' in $file (oder er ist leer)." >&2
  exit 1
fi
printf '%s\n' "$notes"
