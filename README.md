<div align="center">

<img src="docs/logo.png" alt="FlexFamily Calendar" width="160" />

# FlexFamily Calendar — Benutzerhandbuch

Familienplaner für Arbeitszeiten, Schichten, Aktivitäten, Krank- und
Urlaubsmeldungen und Schichttausch — für Eltern, Kinder, Angestellte und Au-Pairs.

</div>

---

## Inhalt

- [Was FlexFamily Calendar für dich macht](#was-flexfamily-calendar-für-dich-macht)
- [App holen](#app-holen)
- [Anmelden](#anmelden)
- [Der Wochenplan](#der-wochenplan)
- [Einen Eintrag anlegen](#einen-eintrag-anlegen)
  - [Arbeit / Schicht](#arbeit--schicht)
  - [Aktivität](#aktivität)
  - [Übernachtung](#übernachtung)
  - [Krank, Urlaub oder Abwesend](#krank-urlaub-oder-abwesend)
- [Wiederkehrende Aktivitäten](#wiederkehrende-aktivitäten)
- [Feiertage](#feiertage)
- [Woche kopieren und finalisieren](#woche-kopieren-und-finalisieren)
- [Schichttausch](#schichttausch)
- [Benachrichtigungen](#benachrichtigungen)
- [Stundenkonto](#stundenkonto)
- [Monatsübersicht](#monatsübersicht)
- [Wochenplan als PDF](#wochenplan-als-pdf)
- [Wochenplan per E-Mail versenden](#wochenplan-per-e-mail-versenden)
- [Dein Profil](#dein-profil)
- [Datenschutz](#datenschutz)
- [Admin-Bereich (nur Eltern)](#admin-bereich-nur-eltern)
- [KI-Unterstützung](#ki-unterstützung)
- [Über und Hilfe](#über-und-hilfe)
- [Für Betreiber und Entwickler](#für-betreiber-und-entwickler)

---

## Was FlexFamily Calendar für dich macht

FlexFamily Calendar hilft Familien mit flexiblen Beschäftigungsverhältnissen dabei,
ihre Woche zu planen — **eine gemeinsame Ansicht** für alle:

- **Eltern** sehen ihren eigenen Kalender und planen die Woche für alle.
- **Kinder** tragen Schule, Kita, Sport und andere Aktivitäten ein.
- **Angestellte** (Haushalt, Betreuung) sehen ihre Schichten, tragen Aktivitäten
  ein und können Krank- oder Urlaubsmeldungen abgeben.
- **Au-Pairs** planen ihre Arbeitszeiten flexibel über den Tag und tragen
  ihre eigenen Termine (Sprachkurs, Freizeit) ein.

Statt getrennter Kalender oder Papierlisten liegt alles zentral — und jede Person
sieht die Sicht, die zu ihrer Rolle passt.

## App holen

FlexFamily Calendar läuft auf zwei Wegen:

### Im Browser

Wenn eure Familie einen eigenen Server hat, öffnest du einfach die Adresse
(z.B. `https://flexfamily.dein-name.de`) in Chrome, Firefox oder Edge und
meldest dich an. Kein Download nötig, funktioniert auf jedem Gerät mit modernem
Browser.

### Als Desktop-App

Für Windows und Linux gibt es fertige Pakete unter
[github.com/Kroste/FlexFamily-Calendar/releases](https://github.com/Kroste/FlexFamily-Calendar/releases/latest):

- **Windows**: die `.zip`-Datei entpacken, `FlexFamilyCalendar.Desktop.exe`
  doppelt anklicken.
- **Linux**: die `.AppImage`-Datei herunterladen, ausführbar machen
  (`chmod +x`), doppelt anklicken.

Die Desktop-App aktualisiert sich beim Start selbst, wenn eine neue Version
verfügbar ist — ein Dialog mit den Neuerungen erscheint, und mit einem Klick
läuft die neue Version.

## Anmelden

Beim ersten Start zeigt die App den Anmeldebildschirm. Trage deinen
**Benutzernamen** und dein **Kennwort** ein.

- **Sprache** oben rechts umschaltbar (Deutsch / English).
- **Login merken**: mit einem Häkchen bleibst du auf diesem Gerät angemeldet,
  bis du dich abmeldest.
- **Verbindung**: unten am kleinen grauen Link kannst du zwischen „Lokal" und
  „Server" wechseln und die Server-Adresse eintragen.

Wenn du deinen Benutzer noch nicht hast, sprich mit dem Elternteil, das den
Kalender verwaltet — die Benutzerverwaltung liegt beim Admin.

## Der Wochenplan

Nach dem Anmelden landest du auf der Hauptansicht: einer Tabelle mit
**Personen als Zeilen** und **Wochentagen als Spalten**.

Die Zeilen sind gruppiert:

1. **Eltern**
2. **Kinder**
3. **Angestellte**
4. **Au-Pairs**

Jede Zelle zeigt, was diese Person an diesem Tag macht — Schichten, Aktivitäten,
Übernachtungen, Abwesenheiten. Farben identifizieren Personen.

Über der Tabelle findest du die Wochen-Steuerung:

- **◀ Vorherige Woche** / **Nächste Woche ▶** — springt eine Woche weiter.
- **Heute** — springt in die aktuelle Woche.
- **Meine Sicht** / **Alle** — Admin schaltet zwischen der eigenen Sicht (nur du)
  und der Familien-Sicht (alle). Nicht-Admins sehen immer nur sich selbst.
- **Stunden** — zeigt einen Balken pro Person mit den Arbeitsstunden dieser Woche.
- **Feiertage** — blendet Feiertage im Kopf der Wochentage ein/aus.

## Einen Eintrag anlegen

Klick auf eine leere Zelle → **Eintrag-Editor** öffnet sich.

Alle Einträge haben eine **Zeit** (von–bis), einen **Typ** und optional einen
**Kommentar**.

### Arbeit / Schicht

Für Angestellte und Au-Pairs. Trage **Startzeit** und **Endzeit** ein — die
gearbeiteten Stunden werden automatisch auf das [Stundenkonto](#stundenkonto)
angerechnet.

**Über Mitternacht arbeiten** (z.B. Au-Pair 20:00–06:00): einfach eintragen, die
App wickelt die Zeit über den Tag hinweg richtig ab. Der Eintrag erscheint bis
Mitternacht am Starttag und optisch gedämpft am Folgetag; die Stunden zählen nur
einmal am Starttag.

### Aktivität

Für alles, was keine Arbeit ist — Schule, Kita, Sport, Sprachkurs, Freizeit.

Wähle eine **Kategorie** aus dem Dropdown (die Kategorien pflegt der Admin für
jede Rolle: Kindern stehen andere Optionen zur Verfügung als Au-Pairs). Die
Farbe der Kategorie erscheint dann in der Zelle.

### Übernachtung

Für Betreuungspersonen, die nachts auf Abruf da sind. Trage den vollen
Zeitbereich ein (z.B. 22:00–07:00). Die Anzeige deckt den ganzen Zeitraum ab
(auch über Mitternacht), aber auf das Stundenkonto werden **pauschal x Stunden
pro Tag** angerechnet (Standard 2 h, vom Admin einstellbar in
Einstellungen → Übernachtung) — nicht die tatsächliche Dauer.

Eine Übernachtung zählt **nicht** als aktive Arbeit — sie ignoriert Tages-,
Wochen- und Ruhezeit-Grenzen, überschneidet sich also nicht mit ihnen.

### Krank, Urlaub oder Abwesend

Krank- und Urlaubsmeldungen laufen **als Zeitbereich** (von–bis). Im
Eintrag-Editor Typ auswählen, Startdatum und Enddatum setzen — die App legt für
jeden betroffenen Tag einen Eintrag an und verknüpft sie miteinander.

Im Wochenplan erscheint deine Abwesenheit als kompakter Hinweis unter dem
Datum. Als Nicht-Admin siehst du bei fremden Personen nur den Hinweis
„Abwesend" ohne Grund — die App maskiert Krank/Urlaub für andere.

Als Angestellte(r) oder Au-Pair kannst du dich für dich selbst krank- oder
urlaubsmelden. Der Admin bekommt automatisch eine Benachrichtigung.

## Wiederkehrende Aktivitäten

Regelmäßige Termine (Sport jeden Dienstag, Kita jeden Werktag, Sprachkurs jeden
Freitag) trägst du nicht jede Woche neu ein — der **Admin pflegt sie als
Wochen-Regel** im Admin-Bereich → Wiederkehrend.

Wiederkehrende Aktivitäten erscheinen als **Overlay** im Wochenplan — sie sind
sichtbar, aber nicht als eigenständige Einträge in der DB gespeichert, sondern
werden aus der Regel projiziert.

Pro Regel entscheidet der Admin, ob sie **an Feiertagen ausfällt** oder ob nur
ein Hinweis „könnte ausfallen" erscheint.

## Feiertage

Feiertage kommen aus dem eingestellten **Bundesland** (Admin → Einstellungen).
Sie erscheinen als roter Hinweis im Kopf des Wochentags im Plan und im PDF.

Wenn du Feiertage nicht sehen willst (z.B. weil du kaum betroffen bist), kannst
du sie in deinem Profil ausblenden — die Feiertagslogik selbst (z.B. dass
wiederkehrende Aktivitäten dort ausfallen) bleibt aktiv.

## Woche kopieren und finalisieren

Im Kalender-Header:

- **Woche kopieren →**: kopiert alle Einträge der aktuellen Woche in die
  nächste. Praktisch für stabile Grundwochen; wiederkehrende Aktivitäten werden
  dabei ignoriert (die kommen ja automatisch).
- **Woche finalisieren**: markiert die Woche als fest — für Angestellte und
  Au-Pairs bedeutet das „die Planung steht". Krankmeldung ist weiter möglich,
  löst dann aber den KI-Umplanungs-Vorschlag aus (siehe unten).

Nur Admin und Eltern können finalisieren.

## Schichttausch

Wenn du eine Schicht hast, aber jemand anderes einspringen soll:

1. Auf deine Schicht klicken → Editor öffnen.
2. **Tausch vorschlagen** wählen und die Zielperson auswählen.
3. Der Vorschlag geht als Benachrichtigung an die andere Person.
4. Die andere Person bestätigt oder lehnt ab. Bei Bestätigung wechselt die
   Schicht automatisch.

Eltern sehen alle Tauschvorgänge im **Admin → Schichttausch**-Bereich und
können sie überstimmen.

## Benachrichtigungen

Die **🔔 Glocke** oben rechts zeigt eine Zahl, wenn ungelesene Benachrichtigungen
warten:

- **Krankmeldung**: eine Person hat sich krank gemeldet — Admin sieht das, kann
  einen KI-Umplanungsvorschlag anfordern.
- **Gesund gemeldet**: eine krank gemeldete Person meldet sich wieder gesund —
  vorgeschlagene Umplanungen können zurückgenommen werden.
- **Schichttausch vorgeschlagen / bestätigt / abgelehnt**.
- **Urlaubswunsch** einer angestellten Person.

Klick auf die Glocke → Liste der Benachrichtigungen. Von dort kannst du direkt
zur betroffenen Woche oder Person springen.

## Stundenkonto

Der Button **Stunden** im Header öffnet dein Stundenkonto:

- **Aktueller Saldo**: laufende Differenz aus Ist-Stunden minus Soll-Stunden,
  seit Konto-Start.
- **Wochenübersicht**: pro Woche die geleisteten und geplanten Stunden mit
  Differenz.
- **Hinweis**: Krank und Urlaub werden als geleistete Stunden angerechnet.

Als Angestellte(r) siehst du nur dein eigenes Konto. Admins sehen alle Konten
und können die Konto-Startzeit + einen Anfangs-Saldo pro Benutzer setzen
(Admin → Benutzer).

## Monatsübersicht

Der Button **Monatsübersicht** öffnet eine Sicht auf einen ganzen Monat:

- Pro Person: **Ist**, **Soll**, **Differenz** in Stunden.
- Pfeile oben schalten zwischen den Monaten.

Praktisch, um am Monatsende zu sehen, wer über oder unter dem vereinbarten
Wochen-Soll liegt.

## Wochenplan als PDF

Der Button **PDF** exportiert die aktuelle Woche als **A4-quer-PDF** mit sieben
Tagesspalten. Das PDF zeigt die Woche **aus deiner Sicht** — Fremde
Krank/Urlaub sind auch im Export als „Abwesend" ohne Grund maskiert.

Datenschutz bleibt also erhalten: du kannst dein PDF unbesorgt weiterreichen.

## Wochenplan per E-Mail versenden

Der Button **Mail** (nur Admin) öffnet einen Dialog mit allen Personen, die eine
E-Mail-Adresse hinterlegt haben. Alle sind standardmäßig ausgewählt — du kannst
Empfänger abwählen.

Wichtig: **Jeder Empfänger bekommt sein eigenes PDF, aus seiner Sicht
maskiert**. Krankmeldungen der anderen erscheinen bei ihm nur als „Abwesend"
ohne Grund — auch wenn du selbst als Admin die Details siehst.

Der Versand läuft einzeln pro Empfänger (kein sichtbares BCC-Feld) und nutzt
die SMTP-Einstellungen aus dem Admin-Bereich.

## Dein Profil

Der Button **Profil** öffnet deine persönlichen Einstellungen:

- **Anzeigename**, **E-Mail-Adresse**.
- **Sprache** (Deutsch / English).
- **Farbe** — die Farbe, mit der du im Plan angezeigt wirst.
- **Kennwort ändern**.
- **KI-Stil-Hinweis** — Freitext, den die KI beim Erstellen von Vorschlägen
  berücksichtigt (z.B. „bevorzugt Vormittage", „arbeitet nicht am Wochenende").
- **Theme** (Hell / Dunkel / System).
- **Feiertage anzeigen** ein/aus.

Angestellte und Au-Pairs sehen nur ihr eigenes Profil. Admin sieht dieselbe
Ansicht — und im Admin-Bereich alle anderen.

## Datenschutz

FlexFamily Calendar ist für die Familie gebaut, aber respektiert die
Privatsphäre der einzelnen Personen:

- **Krank/Urlaub-Grund** ist nur für den Betroffenen selbst und für Admins
  sichtbar. Alle anderen sehen den Zeitbereich als „Abwesend" ohne Detail.
- Die Maskierung greift **im Plan, im PDF-Export und im E-Mail-Versand** —
  jeder Empfänger bekommt seine eigene, maskierte Sicht.
- **Passwörter** werden serverseitig mit BCrypt gehasht (nie im Klartext
  gespeichert). Auf dem Desktop bleibt dein gemerktes JWT-Token verschlüsselt
  im lokalen Speicher.
- **SMTP- und KI-API-Schlüssel** liegen im Server-Modus ausschließlich
  serverseitig als Umgebungsvariablen — der Browser sieht sie nie.

## Admin-Bereich (nur Eltern)

Als Admin öffnet der Button **Admin** einen Dialog mit fünf Tabs:

- **Benutzer**: neue Personen anlegen (Rolle, Kategorie, Farbe, Wochenstunden,
  Übernachtungs-Pauschale, KI-Stil-Hinweis, Konto-Startzeit, Anfangs-Saldo),
  bearbeiten, löschen. Kinder bekommen keinen Passwort-Zwang (Kind-Konten
  ohne Anmeldung sind möglich).
- **Kategorien**: Aktivitätstypen anlegen (Name, Farbe, für welche Rollen sie
  auswählbar sein sollen). Beispiel: „Sport" für Kinder, „Sprachkurs" für
  Au-Pair, „Home Office" für Eltern.
- **Wiederkehrend**: Wochen-Regeln pflegen — Wochentag, Startzeit, Endzeit,
  Kategorie, Person, „an Feiertagen ausfallen" ja/nein.
- **Einstellungen**: Bundesland (für Feiertage), Übernachtungs-Pauschale in
  Stunden pro Tag, SMTP-Server (nur wenn lokal — im Server-Modus liegt SMTP
  in ENV), Update-Prüfintervall.
- **KI**: aktiven KI-Provider auswählen (siehe unten).

## KI-Unterstützung

FlexFamily Calendar kann eine KI beim Planen einbeziehen — additiv, nie im
kritischen Pfad. Die App funktioniert auch ohne KI voll.

Verfügbare Provider:

- **Anthropic Claude** (Cloud)
- **OpenAI / ChatGPT** (Cloud)
- **Google Gemini** (Cloud)
- **Perplexity** (Cloud)
- **Ollama / Llama** (lokal — datenschutzfreundlich, Default wenn erreichbar)

**Wozu die KI genutzt wird:**

- **Krankmeldung → Umplanungsvorschlag**: wenn eine finalisierte Woche
  aufgebrochen werden muss, schlägt die KI vor, wer die betroffenen Schichten
  übernimmt.
- **KI-Chat im Planer**: freie Fragen an den Plan (z.B. „Wer arbeitet am
  Wochenende zusammen mit mir?").

Die KI-Vorschläge sind **immer nur Vorschläge** — nichts geht ohne
Bestätigung durch Admin oder Betroffene live.

Im Server-Modus liegen die API-Schlüssel serverseitig — du siehst im
KI-Tab nur die Provider-Auswahl und optional den Modellnamen.

## Über und Hilfe

Der Button **ℹ** oben rechts öffnet die Info-Box mit:

- App-Name und Version
- Kurzbeschreibung
- **GitHub-Link** zum Repository
- **☕ Buy me a coffee** — Unterstützung für den Entwickler

Fehler oder Wunsch? Ein Issue auf
[github.com/Kroste/FlexFamily-Calendar/issues](https://github.com/Kroste/FlexFamily-Calendar/issues)
mit einer kurzen Beschreibung reicht.

---

## Für Betreiber und Entwickler

Wenn du FlexFamily Calendar selbst auf einem Server betreiben oder am Code
mitwirken willst, findest du die Betrieb- und Entwicklerdokumentation in
[CLAUDE.md](CLAUDE.md) (Projektkanon) und in
[docs/DEPLOY.md](docs/DEPLOY.md) (Docker-Compose-Setup mit Postgres, Caddy und
Watchtower).

Kurzstart:

```bash
# Desktop lokal starten
dotnet run --project desktop/FlexFamilyCalendar.Desktop.csproj

# Tests
dotnet test FlexFamilyCalendar.slnx
```

**Anforderungen:** .NET 10 SDK, für den WASM-Build zusätzlich
`dotnet workload install wasm-tools`.

Auf jedes Tag `vX.Y.Z` läuft `.github/workflows/release.yml` und baut fünf
Artefakte parallel: Windows-ZIP, Linux-Tar, Linux-AppImage und zwei
Docker-Images (API + Caddy mit eingebetteter WASM-SPA).

**Lizenz:** siehe [LICENSE](LICENSE).
