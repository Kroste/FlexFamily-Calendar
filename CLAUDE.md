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
- **Die Wochenansicht lädt über `IStorageService.LoadDaysAsync(from, to)`, nicht sieben Mal
  `LoadDayAsync`.** Im Server-Modus sind das zwei Anfragen (`/api/entries?from&to` und
  `/api/day-notes?from&to`) statt vierzehn — auf Mobilfunk dominiert die Latenz, nicht die
  Datenmenge. Die Schnittstelle bringt eine Default-Implementierung mit, die tageweise parallel
  lädt; nur `ApiStorageService` überschreibt sie. Abwesenheiten sind serverseitig EIN
  Bereichs-Eintrag und werden vom Client über `EntryMapping.CoversDay` auf die Tage verteilt.
  Der Notiz-Bereichsabruf hat einen Fallback auf tageweises Laden: ein Client, der vor dem Server
  aktualisiert wurde, trifft sonst auf einen Endpunkt, den seine Server-Version nicht kennt.
- **Die Finalisierungs-Sicht hängt am STARTTAG des Eintrags, nicht am abgefragten Fenster.**
  `/api/entries` lädt den Finalisierungs-Status deshalb ab `min(from, frühester Starttag)`.
  Vorher stand dort schlicht `from`: eine Abwesenheit von Montag bis Freitag war für Kollegen nur
  am Montag zu sehen und verschwand ab Dienstag, weil ihr Starttag beim tageweisen Abruf außerhalb
  des Fensters lag und damit als „nicht freigegeben" galt.
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
- **Kein blockierendes Warten auf Netz im Start-Pfad der SingleView-Heads.** Browser und Android
  setzen die MainView sofort und melden den gemerkten Benutzer danach über `AutoLoginAsync` an.
  `GetRememberedUserAsync` ruft im Server-Modus `/api/auth/me` auf; blockierend abgewartet hing der
  UI-Thread am Netz, und Android beendet eine App, die keine 5 Sekunden auf Eingaben reagiert (ANR)
  — auf dem Handy ist Funkloch der Normalfall, nicht die Ausnahme. Der Desktop darf blockieren
  (eigener Prozess, kein Watchdog), WASM kann es gar nicht (Single-Thread).
- **`ApiClient` setzt ein Zeitlimit von 30 s** (`RequestTimeout`). Der `HttpClient`-Default sind
  100 Sekunden — so lange bleibt bei stummem Server jede Aktion hängen, ohne erkennbaren Grund.
- **KI ist additiv, nie im kritischen Pfad** — die App funktioniert ohne KI voll. Im Server-Modus
  liegt der API-Key serverseitig in ENV (`ApiAiProvider`), lokal über die Einstellungen.
- **PDF-Export:** eigener abhängigkeitsfreier PDF-Writer (reines Managed). **Keine native
  PDF-/Skia-Lib hinzufügen** — QuestPDF kollidiert in-process mit Avalonias SkiaSharp.
  Der Writer erzeugt **mehrere Seiten**: `SplitIntoPages` verteilt die Personenzeilen, jede
  Folgeseite wiederholt den Spaltenkopf, die Hinweiszeile steht nur auf der letzten. Die
  Objektnummern in `Assemble` hängen an der Seitenzahl — beim Erweitern mitzählen, sonst
  passen `xref`-Anzahl und `trailer/Size` nicht mehr und strenge Betrachter verweigern die
  Datei. Vorher gab es genau eine Seite, und Personen, die nicht mehr draufpassten, fehlten
  ersatzlos.
- **Lokale JSON-Ablage läuft ausschließlich über `JsonFileStore`:** atomar schreiben
  (`.tmp` + `File.Move(overwrite)`) und defekte Dateien nach `.broken` sichern statt sie beim
  nächsten Save zu überschreiben. Quarantäne **nur** bei `JsonException`, nicht bei
  `IOException` — bei gesperrter Datei ist der Inhalt intakt, ein Verschieben würde gute Daten
  wegräumen.
- **Keine Farbliterale im XAML.** Alle Farben liegen als Rollen-Keys in
  `src/Styles/Palette.axaml` (`DangerBrush`, `SuccessBrush`, `ScrimBrush`, …). Tote
  `{DynamicResource …}`-Verweise scheitern in Avalonia **still** — das Element rendert einfach
  falsch. `ResourceKeyTests` gleicht referenzierte gegen definierte Keys ab und schlägt bei
  neuen Literalen an; Fluent-Keys sind am `System`-Präfix erkennbar und ausgenommen.
  **Oberflächen-Rollen liegen in `ThemeDictionaries` (Light/Dark)**, weil die App Hell/Dunkel/
  System anbietet — der Kroste-Look ist fix dunkel und wäre hier unbrauchbar. Eine Rolle, die
  nur in einem der beiden Themes steht, fällt im anderen still aus; ein Test hält deshalb fest,
  dass Light und Dark denselben Satz definieren. Statusfarben (Rot, Grün, Orange) bleiben
  theme-neutral.
- **Style-Bibliothek statt Inline-Optik:** `src/Styles/AppStyles.axaml` bringt Button-Default
  plus `.accent`/`.ghost`/`.danger`, `Border.card`/`.card-flat`,
  `TextBlock.h1`/`.h2`/`.section-label`/`.muted`/`.secondary`, dünne ProgressBar und
  `Rectangle.divider-h`/`-v`. Neue Views nutzen diese Klassen; bestehende werden **beiläufig**
  nachgezogen, wenn sie ohnehin angefasst werden — der Kroste-Standard verlangt ausdrücklich
  keinen separaten Refactor-Ritt, sonst driftet der Look während des Umbaus auseinander.
  Selektoren auf Template-Interna (`/template/ Border#PART_…`) scheitern bei falschem Namen
  **still**, und die Fluent-Templates liegen als kompiliertes XAML vor, sind also nicht
  nachschlagbar. Die Namen kommen deshalb aus der laufenden App: `GET /elements` der
  Design-Test-API listet auch Template-Interna. So sind die beiden Namen für den Fokus-Ring
  belegt (`PART_BorderElement` für TextBox, `Background` für ComboBox) — bei Änderungen
  genauso nachprüfen, nicht raten.
- **Sprachwechsel über `LocalizedString`**, nicht über Indexer-Binding. Ein
  `PropertyChanged("Item[]")` ist die WPF-Konvention und wird von Avalonia 12 nur unzuverlässig
  verarbeitet: Fenster ohne Fokus bleiben stale. Die Wrapper liegen in einem **statischen**
  Cache — Avalonia hält `Binding.Source` nicht stark, ein pro Binding erzeugter Wrapper wäre
  nach dem ersten Rendering weg.
- **JSON:** Ausschließlich `System.Text.Json` mit gemeinsamem `JsonOptions.Pretty`
  (PropertyNamingPolicy=null, damit alte PascalCase-JSON-Files weiter lesbar bleiben);
  Newtonsoft.Json ist als Dependency raus.
- **Benachrichtigungen und Schichttausch sind Einzel-Operationen, kein Replace-all.**
  `IStorageService` hat dafür `LoadNotificationsAsync(userId)`/`AddNotificationsAsync`/
  `MarkNotificationsReadAsync` und `CreateSwapRequestAsync`/`AcceptSwapRequestAsync`/
  `RejectSwapRequestAsync`/`WithdrawSwapRequestAsync`; die alten `Save…Async(List)` sind bewusst
  weg, damit kein Pfad sie wieder benutzt. Vorher lieferte `GET /api/notifications` ALLE
  Benachrichtigungen aller Nutzer an jeden (darunter „X hat sich krank gemeldet" — Gesundheitsdaten,
  genau was die Maskierung verbirgt), und jeder Client konnte beide Tabellen per PUT komplett
  ersetzen. Jetzt serverseitig:
  - Benachrichtigungen: Lesen nur eigene. Anlegen auch für andere (das ist der Zweck), aber
    Nicht-Admins nur die Schlüssel ihrer eigenen Abläufe (`NotificationRules.KeysForEveryone`:
    Tausch + Krankmeldung) — sonst ließe sich „Deine Schicht wurde entfernt" vortäuschen.
    Id, Zeitstempel und Gelesen-Status setzt der Server.
  - Schichttausch: sehen nur Beteiligte + Admin (`SwapRules.CanSee`), Außenstehende bekommen 404.
    Anbieten nur die eigene Schicht (Admin darf im Namen anderer — KI-Planer); Namen und Datum
    nimmt der Server aus der DB, nicht vom Client. **Annehmen macht der Server**
    (`POST /api/swap-requests/{id}/accept`): prüft wie `ShiftSwapEngine.Validate` (stale,
    finalisiert, Überschneidung) und bucht beide Schichten plus Status in EINEM `SaveChanges` um.
    Vorher schrieb der Client die `UserId` am Eintrag um und speicherte den Tag — das
    Eintrags-Update der API trägt aber gar keine `UserId`: der Tausch stand auf „angenommen",
    die Schicht wechselte nie den Besitzer (Mitarbeiter bekamen gleich 403).
  - Der lokale Modus und der Test-Fake teilen sich `ListBackedCollaboration`, damit beide gleich
    reagieren.
  Kein `BeginTransaction` in den Endpunkten: der EF-InMemory-Provider der Integration-Tests
  kennt keine Transaktionen, ein einzelnes `SaveChanges` ist auf Postgres ohnehin atomar.
- **Dialoge, deren Aktion scheitern kann, führen sie aus, SOLANGE sie offen sind** (Muster:
  `ShiftSwapViewModel` mit `execute`-Callback → `CalendarViewModel.ExecuteSwapAsync`). Fehler
  kommen als anzeigefertiger Text zurück, der Dialog bleibt offen und zeigt ihn; die Knopfleiste
  ist währenddessen über `IsBusy` gesperrt. Vorher schloss der Tausch-Dialog zuerst und der
  Aufrufer führte danach aus — ein gescheitertes Annehmen landete als `LogService.Warn` in der
  Statuszeile und wurde vom anschließenden `LoadWeekAsync` („Lade Kalenderwoche …" als Info)
  nach Millisekunden überschrieben. **Die Statuszeile taugt nicht als Fehlermeldung für eine
  Nutzeraktion**: jede folgende Info überschreibt sie. Benachrichtigungen nach einer gelungenen
  Aktion laufen gekapselt (`NotifyQuietlyAsync`) — ein Sendefehler dort darf die schon gebuchte
  Aktion nicht als gescheitert melden, sonst versucht der Nutzer es erneut und bekommt
  „bereits erledigt".
- **Tagesnotizen filtert der Server** (`DayNoteVisibility`): eine adressierte Notiz bekommen nur
  der Admin und die angesprochene Person; alle anderen erhalten Text UND Adressat leer. Vorher
  lieferten beide GET-Endpunkte jede Notiz an jeden aus und erst `CanSeeNote` im Client blendete
  sie aus — in der Web-Ansicht lag eine persönliche Notiz damit im Netzwerk-Tab. Der
  Client-Filter bleibt für die Impersonate-Sicht.
- **Datenschutz:** Fremde Krank-/Urlaubsgründe erscheinen nur als „Abwesend" (Maskierung pro Betrachter),
  im Plan, PDF und Mail-Versand (je Empfänger aus dessen Sicht).
- **Titelleiste:** Die Drag-Fläche trägt `chrome:WindowDecorationProperties.ElementRole="TitleBar"`,
  die drei Fensterbuttons `="User"` — **nicht** `MinimizeButton`/`MaximizeButton`/`CloseButton`,
  diese Rollen laufen über den Non-Client-Pfad, wo DWM die Klicks schlucken kann. Zusätzlich
  prüft der managed `PointerPressed`-Handler über `LandedOnInteractiveChild`, ob der Klick auf
  einem interaktiven Control gelandet ist. Der Guard löst ein anderes Problem als die Rollen
  (managed Fallback statt OS-Hit-Test) und darf nicht als „dank ElementRole überflüssig"
  entfernt werden: sobald ein Umschalter oder Suchfeld in die Leiste kommt, startet sonst jeder
  Klick darauf einen Fenster-Drag und das Dropdown öffnet nie.
  `ExtendClientAreaTitleBarHeightHint` steht bewusst auf `32` statt dem im Skill genannten `-1`:
  der Wert passt exakt zur Höhe der gerenderten Leiste, und das Symptom „tote Titelleiste",
  gegen das `-1` hilft, liegt hier nicht vor.
- **Pointer-Capture in der Plantabelle NIE ohne laufenden Drag zurücknehmen.** `OnRowLoaded`
  hängt die Drag-Handler mit `Tunnel | Bubble` und `handledEventsToo` an die Personenzeile —
  sie laufen damit **vor** allem, was darunter liegt. Ein `e.Pointer.Capture(null)` im
  Release-Handler schickt jedem Button in der Zeile ein `PointerCaptureLost`; Avalonias Button
  setzt darauf `IsPressed=false` und verwirft seinen Click. Genau daran waren der „+"-Knopf
  der Tageszelle und der Impersonate-Knopf tot: `OnCellTapped` blendet Buttons bewusst aus
  (sonst doppelter Dialog), der Click kam nie an, und es passierte schlicht gar nichts —
  ohne Fehlermeldung. `OnRowPointerReleased` steigt deshalb bei `!_rowDragStarted` sofort aus.
  Der Zustand wird zudem **vor** dem `await` zurückgesetzt: der Handler läuft pro Release
  zweimal (Tunnel und Bubble), sonst liefe der Reorder doppelt.
- **Die Plan-Kachel färbt sich nach der Art des Eintrags, nicht nach der Person**
  (`EntryColors.Tile`: eigene Farbe schlägt Typ, `CalendarEntry.TileColor`). Die Plansicht ist eine
  Leitungssicht — gefragt ist, wer arbeitet, wer frei hat und wer unterwegs und damit nicht
  verfügbar ist; wem die Zeile gehört, steht links daneben. `OwnerColor` bleibt für den
  Personen-Punkt in der Namensspalte und das View-as-Banner. **Gerechnet wird auf `DisplayType`,
  nie auf `Type`** — sonst verriete das Rot einer Krankmeldung den Grund, den die Maskierung
  gerade als „Abwesend" verbirgt; `PlanExport.CellEntry` leitet seinen `displayType` je Empfänger
  selbst ab und färbt danach.
- **Die frei gewählte Kachelfarbe (`CalendarEntry.Color`) ist maskierungspflichtig — an ZWEI
  Stellen.** Serverseitig räumt `EntryDto.Mask` sie weg (wie Typ, Notiz und Uhrzeit); clientseitig
  zählt sie in `TileColor` nur, solange `DisplayType == Type`, und `PlanExport.CellEntry` prüft
  dasselbe gegen den je Empfänger neu abgeleiteten Typ. Ohne das wäre eine fremde Krankmeldung
  trotz „Abwesend"-Label an ihrer Sonderfarbe von einer echten Abwesenheit zu unterscheiden — die
  Maskierung wäre über die Optik unterlaufen. Der lokale Modus hat keinen Server, der maskiert,
  deshalb reicht die Server-Seite allein nicht.
- **`CalendarEntry.DisplayType` fällt ohne gesetzten Wert auf `Type` zurück** (nullable Backing-Feld).
  `EntryType.Work` ist der Enum-Wert 0 und wäre sonst die stille Vorgabe für jeden Eintrag, der
  noch nicht durch `ApplyEntryDisplay` gelaufen ist — Farbe und Label einer Schicht für etwas,
  das keine ist.
- **`EntryWriteRules.NormalizeColor` säubert die Farbe serverseitig** (`#RGB`/`#RRGGBB`, sonst null).
  Ungültiges wird verworfen statt den Request abzulehnen: ein Farbwert ist nebensächlich, ein
  deswegen verlorener Eintrag nicht. Gefiltert wird, weil der Wert ungeprüft in die Oberfläche
  jedes Betrachters wandert.
- **Schriftfarbe auf der Kachel wird gerechnet, nicht gesetzt** (`EntryColors.OnTile`: WCAG-Kontrast
  gegen Schwarz und Weiß, der bessere gewinnt). Sobald eigene Farben im Spiel sind,
  ist jede feste Helligkeitsschwelle irgendwann die falsche. `PdfExportService.TextColor`
  delegiert an dieselbe Funktion — vorher hatte es eine eigene Schwelle (0.62 auf
  0.299/0.587/0.114) und wich bei mittleren Farben von der Bildschirmdarstellung ab.
- **Serien (`RecurringActivity`) tragen eine Freitext-Bezeichnung (`Title`), keine Kategorie.**
  Bis v0.18 WAR die Kategorie der Name — der Dialog setzte `Title` nie, deshalb sah auch der
  KI-Planer bei jeder Serie nur „(ohne Titel)". Beim Umbau wurde der Kategoriename als Titel
  übernommen: serverseitig in der EF-Migration `RecurringFreeTextTitle` (SQL vor dem DropColumn,
  gegen ein Wegwerf-Postgres 17 mit allen Fällen geprüft — Groß-/Kleinschreibung der Id, eigener
  Titel hat Vorrang, gelöschte Kategorie), lokal in `LegacyCategoryMigration.ApplyToRules` beim ersten Laden.
  Dafür liest `RecurringActivity.LegacyActivityTypeId` das alte JSON-Feld `ActivityTypeId` und
  wird ab dann nicht mehr geschrieben (`WhenWritingNull`). Die Übernahme liest die Namen aus der alten `activity-types.json` (`LegacyCategory`), die deshalb liegen bleibt.   Serien haben wie Einzeleinträge eine freie Kachelfarbe (`RecurringActivity.Color`, Migration
  `RecurringColor`, serverseitig über `EntryWriteRules.NormalizeColor` gesäubert). Die
  Projektion schreibt sie in `CalendarEntry.Color` jeder Kachel — damit gilt dieselbe Rangfolge
  und dieselbe Maskierungsprüfung wie bei einer am Eintrag gewählten Farbe, ohne Sonderweg.
- **EF-Migrationen, die Daten umschreiben, gehören gegen echtes Postgres geprüft.** Die
  Integrationstests laufen auf EF-InMemory, das keine Migrationen ausführt. Vorgehen: über den
  Host ein Wegwerf-Postgres starten
  (`host-spawn podman run -d --rm --name ffc-migtest -e POSTGRES_PASSWORD=test -e POSTGRES_DB=ffc -p 127.0.0.1:55432:5432 docker.io/library/postgres:17`),
  mit `ConnectionStrings__Default=…` per `dotnet ef database update <vorige Migration>` auf den
  Live-Stand bringen, Testdaten per `psql` anlegen, auf die neue Migration heben, Ergebnis
  abfragen, `Down` prüfen, Container stoppen.
- **Keine Kategorien, keine Typ-Auswahl (v0.21).** Ein Eintrag heißt, wie man ihn nennt: die
  Freitext-Bezeichnung (`Title`, serverseitig `CategoryLabel`) ist der Name im Plan —
  `CalendarEntry.ShowsTitleAsName`, im PDF genauso. Nur Abwesenheiten heißen nach ihrem
  (maskierten) Typ, mit der Bezeichnung als Vermerk darunter; Alt-Einträge ohne Bezeichnung zeigen
  weiter ihr Typ-Label. Der Dialog hat zwei Modi (`EntryEditorViewModel.IsAbsenceMode`): **Eintrag**
  (Bezeichnung Pflicht, mit Vorschlägen aus Woche und Serien — Abwesenheits-Vermerke bewusst
  NICHT, die sind privat) und **Abwesenheit** (Urlaub/Krank/Abwesend, Von/Bis). Den Umschalter
  sieht nur, wer beides anlegen darf und neu anlegt; Selbst-Antrag = direkt Abwesenheit; beim
  Bearbeiten steht der Modus durch den Eintrag fest. **Neue Einträge sind intern `Work`** —
  Stundenkonto, Tausch (nur Arbeitsschichten), Ruhezeit-Warnungen und die Freigabe-Regel hängen
  am Typ und laufen so unverändert weiter, bis das Stundenkonto umgebaut ist (es rechnet laut Lars
  anders als angenommen). Folge bis dahin: auch „Sprachschule" zählt als Arbeitszeit. Bestehende
  Einträge behalten beim Bearbeiten ihren Typ (Aktivität, alte Übernachtung …).
  Kategorien sind komplett raus (Admin-Reiter, `IStorageService`, API, Tabelle). Die EF-Migration
  `DropCategories` überträgt vorher Name → `CategoryLabel` und Farbe → `Color` (nur wo leer, nur
  gültiges Hex), gegen Postgres 17 mit allen Fällen geprüft; lokal macht das
  `LegacyCategoryMigration.ApplyToEntries` tageweise beim ersten Laden eines Tages.
  **`GET /api/activity-types` bleibt als Stub (leere Liste) stehen:** Clients vor v0.21 laden die
  Kategorien per `Task.WhenAll` zusammen mit der Woche — ein 404 ließe dort den ganzen Plan leer,
  und Handys aktualisieren sich nicht selbst. Allgemein: **Endpunkte, die alte Clients im
  Lade-Pfad abfragen, nie ersatzlos entfernen**, sondern erst als harmlosen Stub stehen lassen.
- **`Clone`-Methoden kopieren Felder einzeln — neue Felder dort nachtragen.** `WeekCopy` und
  `EntryMoveCopy` hatten `Color` seit v0.16 nicht übernommen: „Woche kopieren" und
  Verschieben/Kopieren einer Schicht verloren still die eigene Kachelfarbe. Dasselbe gilt für
  `AllDay`: ohne das Feld wird aus einem kopierten „Frei, ganztägig" eine Schicht 00:00–00:00,
  die 24 Stunden zählt. Mehrtägige Einträge kopieren/ziehen beide gar nicht (`IsMultiDay`).
- **Zeitfelder im Eintrag-Dialog starten leer** (`TimeSpan?` ohne Vorbelegung). Leere Felder
  fängt `EntrySpans.Resolve` ab — außer bei „Ganztägig", das keine Uhrzeit braucht.
- **Start/Ende wie im Google-Kalender (v0.22):** Einträge UND Abwesenheiten haben Start und
  Ende mit Datum und Uhrzeit plus „Ganztägig". Wie gespeichert wird, entscheidet allein
  `EntrySpans.Resolve` (rein, getestet):
  - **Einzeleintrag** (ein Tag, mit Uhrzeit oder ganztägig) wie bisher.
  - **Nachtschicht** (Ende am Folgetag vor der Startzeit, oder am selben Datum 20:00–06:00
    getippt) bleibt EIN Eintrag am Starttag mit `EndTime < StartTime` — bewusst nicht als
    Zeitraum, sonst zerfiele sie in zwei Kacheln, und Tausch/Ruhezeit kennen sie als eine Schicht.
  - **Zeitraum** (mehrtägig, oder jede Abwesenheit): tageweise über `EntrySpans.Build`, verbunden
    über `AbsenceGroupId`/`AbsenceStart`/`AbsenceEnd` (Namen historisch, stehen so in den
    JSON-Dateien). Jeder Tag trägt in `StartTime`/`EndTime` nur **seinen Anteil** (erster Tag
    bis 24:00 = `EntrySpans.EndOfDay`, Mitte 00:00–24:00, letzter ab 00:00), die Eckzeiten des
    Ganzen stehen in `SpanStartTime`/`SpanEndTime` — daraus öffnet der Dialog den ganzen
    Zeitraum von jedem Tag aus, und `EntryMapping.ToCreateBody` schickt sie an den Server.
    `TimeRange` beschriftet die Anteile („ab 14:00", „ganztägig", „bis 10:00").
  - **24:00 kennt `TimeOnly` nicht.** Wo ein Tagesanteil zum Server geht, klemmt
    `ClampToDay` auf 00:00 — ein `TimeOnly.FromTimeSpan(24h)` wirft.
  - **Ein Zeitraum, der genau um Mitternacht endet, hat am letzten Tag keinen Anteil**
    (`EntrySpans.IsEmptyLastDay`). `CoversDay` filtert ihn beim Laden UND im Abgleich von
    `ApiStorageService.SaveDayAsync` weg — fehlt der Filter dort, steht der Server-Eintrag an
    diesem Tag ohne Gegenstück da und wird beim Speichern des Tages gelöscht, samt aller übrigen
    Tage (`ApiSpanSyncTests` gegen eine Fake-API).
  - **Server:** Schema unverändert. Ganztägig = `StartTime`/`EndTime` null (nur paarweise
    erlaubt, für jeden Typ); Zeitraum = `Date`..`EndDate` mit Uhrzeit am ersten und letzten Tag.
    `Corresponds` gleicht Zeiträume über Benutzer/Typ/Datum/Eckzeiten ab, nicht über die Id.
  - **`AllDay` ist nullable:** null = Altbestand, dort galten Abwesenheiten als ganztägig und
    alles andere als Eintrag mit Uhrzeit (`IsAllDay`).
  - **Stunden:** ganztägig zählt 0, ein Zeitraum je Tag seinen Anteil (vereinbart bis zum Umbau
    des Stundenkontos). Vorher zählten ganztägige Krank-/Urlaubstage über `DurationHours` als
    „00:00–00:00 über Mitternacht" = **24 h je Tag**.
  - **Arbeitszeit-Regeln, KI-Prüfung und Tausch** arbeiten nur auf `IsShift` (ein Tag, mit
    Uhrzeit) bzw. `!IsMultiDay`; der Server lehnt den Tausch mehrtägiger Einträge in
    `SwapRules.CheckCreate` ab. Die Handy-Meldung (Krank/Urlaub) bleibt ganztägig.
- **Design-Test-API (`desktop/DesignApi/`, nur Desktop):** lokale REST-Schnittstelle, mit der
  sich UI-Änderungen prüfen lassen — Zustand lesen, Theme und Sprache umschalten, Fenster
  öffnen, Screenshot per `RenderTargetBitmap`. Fernsteuerung von außen
  (`SetForegroundWindow`/`mouse_event`/`PrintWindow`) ist ausdrücklich das falsche Mittel:
  Verhaltens-AV blockiert das Muster, und DPI-Skalierung verschiebt Klick-Koordinaten.
  **Standardmäßig aus**, nur mit `--api-port`, nur Loopback, ohne Bearer-Token antwortet
  jede Anfrage mit 403. `/click` ist zusätzlich hinter `--api-allow-clicks`, und
  `DestructiveGuard` lässt **nur ausdrücklich als unbedenklich gelistete Namen** durch —
  umgekehrt zum Skill-Vorbild, weil die App im Server-Modus an der Live-DB hängt und Mails
  verschickt. Ein Test erzwingt, dass jeder Command des MainWindowViewModel in genau einer
  der beiden Listen steht.

  ```bash
  FlexFamilyCalendar.Desktop --api-port 8765 --api-token geheim --auto-shutdown-after 10m
  curl -s --noproxy '*' -H "Authorization: Bearer geheim" http://127.0.0.1:8765/state
  curl -s --noproxy '*' -H "Authorization: Bearer geheim" \
       -X POST "http://127.0.0.1:8765/theme?variant=Dark" 
  curl -s --noproxy '*' -H "Authorization: Bearer geheim" \
       -X POST "http://127.0.0.1:8765/screenshot?target=main" -o shot.png
  ```
- **Emoji-Fallback (nur Desktop):** `BuildAvaloniaApp` setzt `FontManagerOptions` mit dem
  Farb-Emoji-Font des Systems. Inter bringt keine Emoji-Glyphen mit. Wichtig: `WithInterFont()`
  setzt die Standardfamilie selbst über dieselben Options, deshalb muss `DefaultFamilyName` dort
  erneut gesetzt werden.
  **Emoji taugen trotzdem nicht als UI-Element:** Browser und Android haben diesen Fallback nicht,
  und in WASM gibt es überhaupt keine Systemschriften, die Avalonia erreichen könnte — eine
  Emoji-Schrift einzubetten kostet Megabytes. In der Web-Ansicht standen deshalb Ersatzkästchen
  statt der Länderflaggen. Die Sprachauswahl zeigt jetzt ein Kürzel-Abzeichen
  (`LanguageOption.Badge`), das überall gleich rendert. Neue Symbole gehören nach
  `Styles/Icons.axaml` als Geometrie, nicht als Emoji ins Label.
- **Zwei `DatePicker` nie nebeneinander.** Der Fluent-DatePicker ist mindestens ~296 px breit
  (Tag, Monat, Jahr als drei Felder); in keinem der Dialoge passen zwei davon in eine Zeile, sie
  schieben sich still übereinander. Von/Bis stehen deshalb untereinander. `DateRangeLayoutTests`
  misst das am gerenderten Layout (headless) in der Breite des jeweiligen Dialogfensters.
- **Icon-Geometrien in `Styles/Icons.axaml` sind Umrisse.** Sie gehören mit `Stroke` +
  `StrokeThickness` gezeichnet, nicht mit `Fill` — sonst füllt Avalonia das äußere Rechteck und
  aus dem Kalender wird ein einfarbiger Klotz.
- **Single-Instance-Guard (nur Desktop):** `SingleInstanceGuard` in `Program.Main`, **vor**
  Avalonia. Ein Zweitstart holt die laufende Instanz nach vorn und beendet sich. Nötig wegen
  Tray-Icon und weil im lokalen Modus zwei Prozesse dieselben JSON-Dateien überschreiben.
  **Die Erkennung MUSS über einen Verbindungsversuch laufen, nicht über die `IOException` des
  `NamedPipeServerStream`-Konstruktors:** unter Linux/macOS bildet .NET Named Pipes auf
  Unix-Domain-Sockets ab und bindet die Datei einfach neu — der Zweitstart bekommt dort seinen
  Server und hält sich für die erste Instanz. In-Process-Tests fangen das nicht (dort wirft der
  Konstruktor); nach Änderungen an `TryClaim` mit zwei echten Prozessen aus einem
  `dotnet publish`-Output gegenprüfen.
  **Nach jeder Verbindung `Disconnect()` OHNE `IsConnected`-Vorprüfung.** Die Probe jedes
  Zweitstarts verbindet sich und legt auf, ohne zu schreiben; unter Windows geht die Server-Pipe
  dabei in den Zustand `Broken`, in dem `IsConnected` false ist. Mit der Vorprüfung wurde genau
  dann nicht getrennt, jedes weitere `WaitForConnectionAsync` warf sofort „Pipe is broken", und
  die Schleife lief im Millisekundentakt samt Log-Eintrag pro Durchlauf — bis die Platte voll
  war. Zusätzlich: Backoff nach Fehlern, nur der erste Fehler einer Serie wird geloggt, nach
  fünf in Folge wird die Pipe-Instanz ersetzt. Das Nach-vorn-Holen beim Zweitstart hat unter
  Windows vorher **nie** funktioniert; die Tests liefen nur auf Linux, wo .NET Named Pipes auf
  Unix-Sockets abbildet und das Problem nicht existiert. Die CI hat dafür den Job
  `Single-Instance (Windows)`.
- **System-Tray (nur Desktop):** Minimieren legt ins Tray, Schließen beendet regulär — kein
  `ShutdownMode`-Umbau nötig, weil nur `Hide()` läuft. `TrayController` MUSS als Feld in `App`
  gehalten werden (sonst sammelt der GC das Icon ein), Restore läuft über den UI-Dispatcher mit
  Guard-Flag (sonst Minimize/Restore-Schleife), und das Setup steckt in try/catch (kein Tray auf
  minimalen Desktops / kaputtem DBus). `Tmds.DBus.Protocol` ist auf die Avalonia-Linie gepinnt.
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

- **Central Package Management:** alle Paketversionen stehen in `Directory.Packages.props`,
  die csproj referenzieren **ohne** `Version`-Attribut. Avalonia MUSS über alle vier Heads
  auf derselben Linie bleiben — Dependabot gruppiert die Pakete deshalb zu einem PR.
  Nach jedem Avalonia-Bump gegenprüfen, dass SkiaSharp managed und native dieselbe Linie
  zeigen (`dotnet list … package --include-transitive | grep -i skia`): eine Drift bleibt
  beim Build unsichtbar und tötet die App erst beim ersten gerenderten Fenster.
- **Beide Dockerfiles kopieren `Directory.Build.props` UND `Directory.Packages.props`**
  einzeln ins Build-Image. Fehlt die zweite Datei, scheitert schon der Restore mit NU1015.
- Das **Android-Projekt (`mobile/`) ist NICHT in `FlexFamilyCalendar.slnx`** — damit
  `dotnet build/test` der Solution ohne `android`-Workload durchgeht. Der Android-Build
  läuft im Release-Workflow als eigener Job (`Android APK`, mit `dotnet workload install android`).
- **Android-VersionCode im Release-Workflow:** `MAJ*1000000 + MIN*1000 + PAT`. Mit dem früheren
  Faktor 100 kollidierte `0.16.100` mit `0.17.0` — Android merkt das nicht und installiert
  stillschweigend die falsche Version. Die Formel muss strikt monoton bleiben, auch gegenüber
  bereits veröffentlichten Codes.
- **Server-DB-Schema** ändert sich über **EF-Migrationen**
  (`server/FlexFamilyCalendar.Api/Migrations/`). Nach Feldänderungen:
  `dotnet ef migrations add <Name>` — Live-DB wird beim Redeploy per Startup-Migrate
  aktualisiert (Retry-Block in `Program.cs`, im Testing-Environment übersprungen).
- **Übersetzung Client↔Server** liegt in `src/Services/Api/*Mapping.cs` (User, Entry,
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
  **Die PUT-Endpunkte der Listen (`activity-types`, `recurring-activities`, `planner-notes`,
  `chat-history`, `swap-requests`, `notifications`) sind damit NICHT testbar:** sie ersetzen
  die ganze Liste per `ExecuteDeleteAsync`, das der InMemory-Provider nicht unterstützt — der
  Aufruf endet im Test mit 500. Für die Lesepfade Daten über `ApiTestFactory.Seed(...)` direkt
  in den DbContext legen. Wer die Schreibpfade testen will, braucht einen relationalen
  Provider (SQLite in-memory) statt EF-InMemory.
  Das Api-Testprojekt referenziert `Microsoft.EntityFrameworkCore.Relational` **explizit** —
  über den Web-SDK-ProjectReference landet die DLL nicht im Test-Output.
- **`.vscode/settings.json` MUSS `dotnet.defaultSolution` auf die `.slnx` setzen.** Ohne den
  Eintrag generiert sich das C# Dev Kit beim ersten Öffnen eine eigene `.sln` im
  Workspace-Cache und hält daran fest — die hier erzeugte enthielt nur `src/`, kein einziges
  Testprojekt. Der Test-Explorer bricht dann mit „Test Run Aborted" und 0 Tests ab
  (`Test Case Record lacks a TestingPlatform UID`), während `dotnet test` auf der
  Kommandozeile alle findet. Diagnose läuft über
  `~/.config/Code/logs/<datum>/window1/exthost/ms-dotnettools.csdevkit/C# Dev Kit - Test Explorer.log`;
  die generierte Solution liegt unter
  `~/.config/Code/User/workspaceStorage/<hash>/ms-dotnettools.csdevkit/`.
- **Tests laufen auf xunit.v3 / Microsoft.Testing.Platform.** Dafür braucht es alle drei
  Teile: den `test`-Runner-Block in der `global.json`, `<OutputType>Exe</OutputType>` in
  beiden Testprojekten und **kein** `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio`.
  Keine VSTest-Flags an `dotnet test` hängen (`--nologo` & Co.) — die reicht es an die
  Test-Exe durch, die sie nicht kennt, und der Lauf endet mit „Es wurden keine Tests
  ausgeführt". Tests gegen globale Singletons (`Localizer`, `SecretService`) gehören in eine
  nicht-parallele Collection: xunit.v3 fixiert die Reihenfolge innerhalb einer Klasse nicht.
  **Dasselbe gilt für die geteilte DB der `ApiTestFactory`:** ein `IClassFixture` hält EINE
  Datenbank für alle Tests der Klasse. Jeder Test braucht deshalb eigene Daten (eigene Woche,
  eigene Ids) statt gemeinsamer Konstanten — sonst finalisiert ein Test den Tag, den der nächste
  unfinalisiert braucht. Lokal fällt das nicht auf, weil die Reihenfolge dort zufällig passt;
  auf dem CI-Runner kippt sie (v0.17.0, Run 33741179155).
- **UI-Input-Tests laufen headless** (`Avalonia.Headless`, `HeadlessTestApp` +
  `HeadlessAppFixture` im Client-Testprojekt). Zwei Fallen, beide kosten sonst Stunden:
  1. Die Test-App MUSS `Palette.axaml`/`Icons.axaml`/`AppStyles.axaml` mitladen. Ein Control,
     dessen Hintergrund an einem toten `DynamicResource` hängt, hat **keinen** Hintergrund und
     ist damit **nicht hit-testbar** — der Klick fällt durch es hindurch auf das Element
     darunter, und der Test prüft klaglos den falschen Pfad (er wird grün, obwohl der Fehler
     drin ist).
  2. `HeadlessUnitTestSession.Dispose()` NICHT aufrufen: es wartet per `_dispatchTask.Wait()`
     auf das Ende der Dispatcher-Schleife, die hier nicht zurückkommt. Der Testprozess läuft
     dann endlos weiter, obwohl alle Tests längst grün sind — sichtbar nur als Lauf ohne
     Zusammenfassung. Die Session sitzt auf einem Thread-Pool-Thread und hält den Prozess
     beim Beenden ohnehin nicht auf.
  Jeder neue Input-Test gehört gegen den kaputten Stand gegengeprüft: läuft er auch ohne den
  Fix grün, misst er etwas anderes als gedacht.
- Versionierung via **MinVer** (Git-Tag `vX.Y.Z`), GitHub-Account **Kroste** (`lars-oste@gmx.de`).

---

## Deploy & CI

- **CI-Workflow** (`.github/workflows/ci.yml`): auf jeden Push/PR `dotnet test --solution FlexFamilyCalendar.slnx`
  (immer mit `--solution` — ältere SDKs brechen die positionale Form unter MTP mit Exitcode 1 ab)
  (installiert vorher `wasm-tools` Workload für den Browser-Head). Zusätzlich der Job
  `Single-Instance (Windows)` auf `windows-latest` — nur die Wächter-Tests, weil Named Pipes
  dort Kernel-Objekte sind und sich grundlegend anders verhalten als unter Linux.
  Per `workflow_dispatch` auch manuell startbar, gegen jeden Branch
  (`gh workflow run CI --ref <branch>`): so lässt sich ein neuer Test gegen den alten Stand
  laufen lassen, ohne `main` rot zu machen.
- **Log-Dateien haben einen Größendeckel** (`archiveAboveSize` 10 MB × `maxArchiveFiles` 30,
  Client und Server). Nur täglich zu rotieren reichte nicht: eine durchdrehende Schleife schrieb
  die Datei bis zum Tageswechsel voll.
- **Jedes Tag braucht einen Abschnitt `## vX.Y.Z` in `CHANGELOG.md` — VOR dem Tag, im selben Push.**
  Der Job `release-notes` holt ihn über `.github/scripts/release-notes.sh` und legt damit das
  Release an; fehlt er, bricht das Release ab (die Artefakt-Jobs hängen per `needs` daran). Der
  Text erscheint in der App im Update-Dialog unter „Was ist neu" (`UpdateService` liest den
  Release-Body). Vorher wurden nur Tags gepusht, softprops legte die Releases mit leerem Text an,
  und der Update-Dialog zeigte ein leeres Feld. Geschrieben für Nutzer: was sich für sie ändert.
  Der Dialog zeigt den Text roh — schlichte `- `-Aufzählungen, kein Fettdruck, keine Links.
  Überschrift-Abgleich ist exakt (`v0.2.0` trifft nicht `v0.20.0`); das Skript lässt sich lokal
  testen: `.github/scripts/release-notes.sh v0.20.1`.
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
  Client **und** Server laufen auf NLog mit eigener `nlog.config`: Datei ab Trace, Konsole ab Info.
  Die Server-Konsole ist das, was `docker logs` zeigt — sie darf nie wegfallen. Log-Verzeichnis
  der API über `FFC_LOG_DIR` (in Compose auf einem Volume, sonst Temp-Fallback).
- **Über die API alles loggen** (Methode/Pfad/Status/Dauer/Benutzer) — das macht die
  `RequestLoggingMiddleware`. Bewusst **ohne Body und ohne Header**: sonst stünde das Passwort
  jedes Logins und jedes JWT im Klartext im Log. In `appsettings.json` steht der Log-Level
  deshalb auf `Trace` — dieser Filter greift VOR NLog, ein `Information` würde die Trace-Regel
  der `nlog.config` wirkungslos machen.
- **Passwörter/Tokens/Secrets NIEMALS loggen** und nie im Klartext ablegen/committen
  (Desktop: `SecretService` mit AES-Keyfile, Browser: Origin-Isolation, Android: Isolated Storage
  über `SecretService`; API-Keys serverseitig in ENV). Als zweite Verteidigungslinie steckt in
  beiden Layouts `${masked:inner=${message}}` — der `MaskingLayoutRenderer` ersetzt
  JSON-Secret-Felder, Connection-String-Passwörter und Bearer-Token. Er registriert sich über
  einen `[ModuleInitializer]`, damit er auch im Testprozess (kein `Main`) vor dem ersten Logger
  steht. **Nicht** über eine `<variable>` einbinden: ob NLog einen Variablenwert erneut als
  Layout interpretiert, ist nicht garantiert, und die Maskierung fiele still aus.
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
- [ ] Alle Fenster über `ChromeWindow`, resizable; InfoBox mit BMC-Button und Update-Prüfung
- [ ] Keine Farbliterale im XAML; neue Farben als Rollen-Key in `Styles/Palette.axaml`
- [ ] Neue Paketversion nur in `Directory.Packages.props`; nach Avalonia-Bump Skia-Linie geprüft
- [ ] README (Nutzersicht) und CLAUDE.md (Entwicklersicht) im selben Commit mitgezogen
- [ ] `CHANGELOG.md`-Abschnitt für das Tag geschrieben (sonst bricht das Release ab)
- [ ] Nach Tag+Push: `gh run list` prüfen; Failures sofort im nächsten Tag reparieren
