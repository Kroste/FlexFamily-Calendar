# Änderungen

Jede Version hat hier ihren Abschnitt `## vX.Y.Z`. Der Release-Workflow übernimmt ihn als Text
des GitHub-Release — und genau dieser Text erscheint in der App im Update-Dialog unter
„Was ist neu". Fehlt der Abschnitt zu einem Tag, bricht das Release ab.

Geschrieben für die Menschen, die die App benutzen: was sich für sie ändert, nicht wie. Der
Update-Dialog zeigt den Text ohne Formatierung — also schlichte Aufzählungen, kein Fettdruck,
keine Links.

## v0.23.0 — 18.09.2026

- Neues Farbkleid in warmen Tönen: Kopf- und Titelleiste in Terrakotta, Creme statt Reinweiß, Knöpfe, Haken und Auswahl passend dazu. Auch der Dunkelmodus ist warm statt blaugrau.
- Die Standardfarben der Kacheln sind wärmer: Ocker für Einträge, Olivgrün für Urlaub, Ziegelrot für Krank. Selbst gewählte Farben bleiben, wie sie sind.
- Das Schließen-Kreuz oben rechts bleibt beim Darüberfahren weiß auf Rot, statt schwarz zu werden.
- Speichern-, Löschen- und Genehmigen-Knöpfe behalten beim Darüberfahren ihre Farbe, statt grau zu werden.
- Die Hinweis-Zeile steht jetzt direkt unter der letzten Person, nicht mehr ganz unten am Fensterrand.
- Im KI-Planer ist die Schrift im Dunkelmodus wieder lesbar.

## v0.22.0 — 18.09.2026

- Einträge und Abwesenheiten haben jetzt Start und Ende mit Datum und Uhrzeit, wie im Google-Kalender. Verschiebst du den Start, wandert das Ende mit.
- Neuer Haken „Ganztägig": dann ohne Uhrzeit, z.B. für „Frei" als ganzen Tag. Bei Abwesenheiten ist er vorbelegt, lässt sich aber abwählen — etwa für einen Arzttermin von 9 bis 11 Uhr.
- Einträge über mehrere Tage: Mittwoch 14:00 bis Freitag 10:00 steht im Plan als „ab 14:00", „ganztägig" und „bis 10:00". Ein Klick auf einen der Tage bearbeitet den ganzen Zeitraum.
- Eine Nachtschicht (z.B. 20:00 bis 06:00 am Folgetag) bleibt ein Eintrag am Starttag, wie bisher.
- Tauschen lassen sich nur Schichten an einem einzelnen Tag.
- Stundenkonto: Ganztägige Krank- und Urlaubstage wurden bisher versehentlich mit 24 Stunden je Tag angerechnet. Ganztägiges zählt jetzt vorerst gar nicht, bis die Berechnung überarbeitet ist.
- Handy-App: auf den Tageskarten steht „ganztägig" statt „00:00-00:00". Die Krank-/Urlaubsmeldung bleibt bei ganzen Tagen.

## v0.21.1 — 18.09.2026

- Ältere App-Versionen zeigen nach dem Server-Update weiter ihren Plan an, statt einer leeren Woche. Bitte trotzdem bald aktualisieren — vor allem die Handy-App.

## v0.21.0 — 18.09.2026

- Der Eintrag-Dialog hat keine Typ-Auswahl mehr. Ein Eintrag bekommt eine frei wählbare Bezeichnung, z.B. „Arbeit", „Frei", „Remise" oder „Sprachschule" — sie steht fett als Name auf der Kachel.
- Beim Tippen der Bezeichnung schlägt die App vor, was in der Woche und bei den wiederkehrenden Aktivitäten schon vorkommt.
- Urlaub und Krankmeldung bekommen einen eigenen Modus: oben im Dialog „Abwesenheit (Urlaub/Krank)" wählen, dann Art und Zeitraum. Genehmigung, Krankmeldung und Datenschutz funktionieren wie bisher.
- Die Kategorien sind entfernt, auch der Reiter im Admin-Bereich. Bestehende Einträge behalten ihren Kategorienamen als Bezeichnung und ihre Kategoriefarbe als eigene Farbe — der Plan sieht also gleich aus.
- „Woche kopieren" sowie das Verschieben und Kopieren einer Schicht übernehmen jetzt auch die eigene Kachelfarbe. Bisher ging sie dabei verloren.
- Hinweis: Neue Einträge zählen vorerst alle als Arbeitszeit. Die Berechnung des Stundenkontos wird noch überarbeitet.

## v0.20.1 — 18.09.2026

- Die Datumsfelder „Von" und „Bis" überlappten sich im Pausen-Dialog der Serien und bei Urlaub/Krank im Eintrag-Dialog. Sie stehen jetzt untereinander.
- Der Update-Dialog zeigt wieder, was in einer neuen Version steckt. Bisher blieb das Feld „Was ist neu" leer.

## v0.20.0 — 18.09.2026

- Wiederkehrende Aktivitäten können eine eigene Kachelfarbe bekommen, genau wie einzelne Einträge. Im Serien-Dialog: Haken „Eigene Farbe für diese Serie", mit Vorschau.
- In der Liste der Serien steht die Farbe als Punkt vor dem Namen.

## v0.19.0 — 18.09.2026

- Wiederkehrende Aktivitäten haben jetzt eine frei eingebbare Bezeichnung statt einer Kategorie, z.B. „Fußball" oder „Sprachschule".
- Bestehende Serien behalten ihren Namen: der bisherige Kategoriename wurde als Bezeichnung übernommen.
- Im Plan steht die Bezeichnung fett als Name auf der Kachel.
- Der KI-Planer kennt jetzt die Namen der Serien (vorher sah er nur „ohne Titel").

## v0.18.1 — 18.09.2026

- Scheitert das Annehmen eines Schichttauschs (Überschneidung, Woche schon finalisiert, Tausch schon erledigt), bleibt der Dialog offen und nennt den Grund. Vorher passierte scheinbar einfach nichts.

## v0.18.0 — 18.09.2026

- Datenschutz: Benachrichtigungen bekommt nur noch ihr Empfänger. Vorher ließen sich über die Schnittstelle die Benachrichtigungen aller abrufen, auch Krankmeldungen.
- Schichttausch funktioniert jetzt auch mit dem Server: nach dem Annehmen wechselt die Schicht wirklich zum Kollegen. Vorher stand der Tausch auf „angenommen", die Schicht blieb aber bei der alten Person.
- Tauschvorschläge sehen nur die beiden Beteiligten und die Admins.
- Der Tausch-Tab auf dem Handy öffnet schneller.
- Wichtig: Ältere App-Versionen können danach keine Benachrichtigungen und Tauschvorschläge mehr speichern. Bitte Desktop und Handy aktualisieren.

## v0.17.3 — 18.09.2026

- Datenschutz: Eine Tagesnotiz an eine einzelne Person sehen nur noch diese Person und die Admins.

## v0.17.2 — 18.09.2026

- Windows: Die App schrieb in manchen Fällen im Millisekundentakt „Instanz-Pipe: Verbindung abgebrochen" ins Log und erzeugte riesige Logdateien. Behoben.
- Windows: Startet man die App ein zweites Mal, holt sie jetzt das bereits offene Fenster nach vorn.
- Logdateien sind auf 10 MB je Datei begrenzt.

## v0.17.1 — 03.09.2026

- Interne Korrektur an den Tests, keine Änderung in der App.

## v0.17.0 — 03.09.2026

- Der Wechsel zwischen Wochen geht deutlich schneller, vor allem auf dem Handy.
- Mehrtägige Abwesenheiten sind für Kollegen an allen Tagen sichtbar, nicht nur am ersten.
- Die Sprachauswahl zeigt „DE" und „EN" statt Flaggen — in der Web-Ansicht standen dort Kästchen.
- Handy: Zweimal „Speichern" bei einer Krankmeldung legt sie nicht mehr doppelt an.

## v0.16.1 — 03.09.2026

- Handy: Die App startet auch ohne Netz sofort, statt zu hängen.
- Antwortet der Server nicht, bricht eine Aktion nach 30 Sekunden ab statt nach fast zwei Minuten.

## v0.16.0 — 03.09.2026

- Beim Anlegen oder Bearbeiten eines Eintrags lässt sich die Kachelfarbe frei wählen, mit Vorschau.

## v0.15.0 — 03.09.2026

- Kacheln im Plan färben sich nach der Art des Eintrags statt nach der Person. So sieht man auf einen Blick, wer arbeitet und wer nicht verfügbar ist.
- Die Schrift auf den Kacheln wird automatisch schwarz oder weiß — je nachdem, was auf der Farbe besser lesbar ist.

## v0.14.4 — 03.09.2026

- Das kleine Plus in einer belegten Tageszelle öffnet wieder den Dialog für einen weiteren Eintrag.
- Die Zeitfelder eines neuen Eintrags starten leer, damit man die Uhrzeit direkt eintippen kann.
- Die Uhrzeit steht fett ganz oben auf der Kachel.

## Ältere Versionen

Siehe Git-Historie.
