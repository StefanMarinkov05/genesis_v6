# Entscheidungsindex

Jede strukturelle Entscheidung liegt als ADR in `docs/adr/`. Diese Dateien sind die Autorität: Kontext, Alternativen, Folgen; eine ersetzte Entscheidung behält ihre Nummer und erhält einen Hinweis. Dieses Kapitel ist der Index.

| ADR | Entscheidung | Gewinn | Kosten |
|---|---|---|---|
| **001** | Drei Dateien: Spezifikation / Lock / State | Expansion ist sicher; Fortsetzen berührt die Eingabe nicht | eine Datei mehr zu verstehen |
| **002** | Identity-Lokalisierung per statischer Karte | deterministisch, kann Razor nicht beschädigen | nur bekannte Zeichenketten |
| **002b** | Kern-Identity-Seiten vollständig ausgegeben | vollständiger bulgarischer Text und Stil | mehr Vorlagen |
| **003** | Symbolische Verweise im Seed | Seeds überleben Läufe | Eltern zuerst (topologisch) |
| **004** | Deterministische Vielfalt (Hash aus Seed + Achse) | vielfältig, stabil pro Projekt | ein Spezifikationsautor kann nicht „randomisieren“ |
| **005** | LLM-Stufen als Dateivertrag | Offline-Modus, Prüfbarkeit, Tests | zwei Dateien pro Austausch |
| **006** | Abweichungen per Regex auf bekannten Formen | billig und laut bei Änderung | spröde per Konstruktion |
| **007** | Soft Delete über globalen Filter | keine Abfrage vergisst ihn | eine Basisklasse mehr |
| **008** | Gestaltung als Token-Schicht | Vielfalt ohne Vorlagenänderungen | Design lebt im Preset |
| **009** | Grenze der Umbenennung | keine Brüche in Interna, Fixtures, Logs | zwei Vokabulare |
| **010** | GUI ist Electron-Hülle über CLI | eine Implementierung | GUI hängt am Logformat |
| **011** | Bild-Cache + Ersatzbilder | schnelle Neuerzeugung, kein Hot-Linking | ein Ordner mehr |
| **012** | Sammelseite „Spezifikationen“ + Filter | Navigation bleibt lesbar | zwei Flags nach Regel |
| **013** | Agent ändert nur die Spezifikation | Ergebnis bleibt reine Vorlagenausgabe | Modell kann Vorlage nicht reparieren |
| **014** | Schreibgeschützte Installationswurzel | Programm schreibt nicht neben sich | zwei Wurzeln für Ressourcen |

## Die drei, die Prinzipien sind

1. **ADR-001 und 005** machen einen Lauf prüfbar: Eingaben unveränderlich, Expansion ein eigenes Artefakt, der Beitrag des Modells eine Datei.
2. **ADR-013 ist die Vertrauensgrenze.** Könnte das Modell erzeugten Code bearbeiten, wäre keine Anwendung erklärbar.
3. **ADR-004 und 008** beantworten, warum zwanzig Anwendungen nicht gleich aussehen — und warum die Antwort deterministisch statt zufällig ist: Die Preset-Wahl verwendet dasselbe SHA-256 wie die Achsen.

## Vorschläge, die der Code noch nicht übernommen hat

Sie sind festzuhalten, bevor diese Ausgabe übernommen wird. (Erledigte frühere Vorschläge: eine Referenz zum Aufbau der erzeugten Anwendung und ein schriftlicher Seed-Vertrag — `docs/explanation/generated-app-anatomy.md` und `docs/reference/seed-format.md`; sowie mit Changelog 141 ein strenges `verify` mit Prüfung auf anonymen Zugriff, funktionierende `cart` und `personalization` und ein reproduzierbares Design-Preset.)

| Vorschlag | Warum |
|---|---|
| Einzeldokumente zugunsten dieses Buches zurückziehen | eine Quelle pro Fakt; heute wiederholen sich Überblick, Tutorial und Referenz |
| `--from spec-build` / `--from spec-expand` laut scheitern lassen | der stille No-op ist die schärfste Kante, die beim Schreiben auffiel |
| Den Namen nach Sprache teilen: Genesis englisch, ein bulgarischer Titel bulgarisch, `dps-nn` als Programm | ADR-009 fixiert einen Namen für beide Ausgaben; zwei Sprachausgaben brauchen zwei |

Eine Entscheidung hinzufügen: eine Datei in `docs/adr/`, nummeriert, mit Kontext / Entscheidung / Alternativen / Folgen; verlinkt von der Stelle, die sie regiert; eine angenommene Entscheidung wird nicht bearbeitet.
