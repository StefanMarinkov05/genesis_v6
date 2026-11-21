# Glossar

Begriffe werden hier einmal definiert; der Rest des Buches verwendet sie nur in dieser Bedeutung.

| Begriff | Bedeutung |
|---|---|
| **Agentenmodus** | Kommando `create`: ein Modell entwirft, die Pipeline läuft, Fehler werden zu Spezifikationsreparaturen. |
| **Admin-Supervisor** | Expansionsschritt, der für jedes Modell ohne Controller ein Admin-CRUD ergänzt. |
| **Automatischer Modus** | LLM-Austausch, den das Werkzeug selbst führt, wenn `ANTHROPIC_API_KEY` gesetzt ist. |
| **Baugate** | `dotnet build`, das eine Stufe bestehen muss. |
| **Checkpoint** | Ein Stufenname, der in der `plan/state.json` des Projekts festgehalten wird, nachdem diese Stufe erfolgreich war. |
| **Codepointer** | Benannter Anker (`searchEngine`, `sorting`, `imageCrudLogic`, …), den `report` zu Datei, Zeile und Auszug auflöst. |
| **Design-Preset** | Benannter Tokensatz (Akzent, Neutraltöne, Schriften, Tiefe) über einem Bootstrap-Theme, in `meta.style.preset` gepinnt oder pro Projekt mit demselben Seed-Hash wie die Achsen gewählt (Kapitel 12). |
| **Abweichung** | Deklarative Änderung an einem Gerüst-Controller: `removedActions`, `redirects`, `sorting`, `pagination`, `ownership`. |
| **`displayName`** | Bulgarische Bezeichnung einer Eigenschaft; Pflicht für jede sichtbare gespeicherte Eigenschaft. |
| **DTO** | Zusammengesetztes Lesemodell über mehrere Entitäten. Verdrahtet, unerprobt. |
| **Feature-Fragment** | Eine JSON-Datei in `templates/features/`, die Modelle (gelegentlich Controller) einbringt, wenn eine Funktion aktiviert ist; die funktionsabhängigen Seiten selbst stammen aus Vorlagen. |
| **Dateivertrag** | Regel (ADR-005), dass jeder LLM-Austausch eine Anfrage- plus Antwortdatei ist. |
| **Gate** | Jede Prüfung, die einen Lauf stoppt: Schema, Build, Migration, `verify`. |
| **Erzeugtes Ergebnis** | Alles unter `out/`. Entbehrlich, nie von Hand bearbeitet. |
| **Idempotente Stufe** | Sicher wiederholbar: erkennt ihre eigene Arbeit. |
| **Installationswurzel** | Schreibgeschützter Ordner des installierten Programms. |
| **`isLookup` / `isSpecification`** | Flags, die kleine Modelle auf die Seite „Spezifikationen“ verschieben. |
| **Sprachgate** | Der Englisch-Scan in `verify`; entfällt bei nicht-bulgarischen Projekten. |
| **Lock-Datei** | `project-spec.lock.json` — die expandierte Spezifikation, die die Stufen lesen. |
| **Manifestprüfung** | Vergleich der zugesagten Dateien mit den vorhandenen. |
| **Manueller Modus** | LLM-Weg ohne Schlüssel: Anfrage wird geschrieben, Exitcode 3. |
| **Eigentum** | Controller-Regel (`{ "userProperty": "UserId" }`), serverseitig erzwungen. |
| **`parentRef_<Target>`** | In einer Seed-Zeile: der `refKey` des Elternteils. |
| **Herkunft** | `generatedBy` eines Controllers: `scaffold`, `template`, `writtenFromScratch`. |
| **`refKey`** | Eindeutiger symbolischer Name einer Seed-Zeile. |
| **Fortsetzen** | Weiterlaufen ab der ersten nicht erledigten Stufe. |
| **Seed** | Die zweite Eingabe: ein Array Zeilen pro Modell. Auch die Stufe. |
| **Seed-Nutzer** | Ein Nutzer, den `IdentitySeeder` aus `identity.seedUsers` anlegt. Sofern die Spezifikation keines setzt, wird sein Passwort aus dem Variation-Seed und der E-Mail abgeleitet; alle stehen in `logs/<Project>/seed-credentials.txt`, und das Banner zeigt einen Admin und einen gewöhnlichen Nutzer. |
| **Spezifikation** | `project-spec.json`, die deklarative Beschreibung der Anwendung. |
| **Stufe** | Ein Schritt der Pipeline: ein `IStage` mit Namen und idempotentem `RunAsync`. |
| **`$seedUser:N`** | Platzhalter in Seed-Zeilen: „der N-te Seed-Nutzer“. |
| **`$primaryModel`** | Token in einem Fragment, ersetzt durch das Hauptmodell des Projekts. |
| **`$variation:axis`** | Platzhalter, den die Expansion durch den aufgelösten Wert ersetzt. |
| **Nicht unterstützte Anforderung** | Eintrag in `meta.unsupportedRequests`. |
| **Variationsachse** | Eine von elf Stilachsen, einmal pro Projekt aufgelöst. |
| **`verify`** | Stufe 8 und die Definition von fertig: Build, Migration, Manifest, Start, `/`, Admin-Anmeldung, die deklarierten Routen (jede muss mit 200 antworten), anonyme Abweisung geschützter Seiten, Sprache. |
| **Arbeitsordner** | Schreibbarer Ordner: `Documents\DPS-NN` installiert, Repository-Wurzel in Entwicklung. |
