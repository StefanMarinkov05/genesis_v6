# GUI-Architektur

Gekürzt aus dem vollständigen Kapitel der englischen Ausgabe.

## Die unverrückbare Regel

Genesis bleibt eine headless CLI. Die GUI hüllt sie als Kindprozess ein und implementiert **niemals** Pipeline-Logik neu. Sie startet `dps-nn run --all`, liest die Ausgabe und steuert den Lebenszyklus. Sie fährt keine Stufen einzeln, baut kein Stufenverhalten nach und verlagert keine Erzeugung nach Node.

## Prozessmodell

| Teil | Verantwortung |
|---|---|
| Hauptprozess | Pfade und Arbeitsordner, IPC, Start der CLI, Verfolgen von `dps-nn.log.jsonl`, letzte Projekte |
| Renderer | Auswahl von Spezifikation und Seed, Fortschritt, Terminal, Einstellungen, Themes |
| preload / shared | schmale Brücke und gemeinsame Typen |

## Der GUI↔CLI-Vertrag

- **Fortschritt wird aus dem Log rekonstruiert**, nicht aus eigenen Nachrichten. Das Format von `dps-nn.log.jsonl` ist ein Vertrag.
- **Zustand** kommt aus `plan/state.json`.
- **Anmeldedaten** kommen aus dem Banner und `project-summary.json`.
- **Fehler** zeigen den Stoppgrund, den die GUI aus der Zeile `agent: stopped - <reason>` auf der Standardausgabe der CLI liest; die vollständige Liste der Versuche bleibt in `agent-report.json`.

## Themes und Oberfläche

Ein Theme ist ein Satz Tokens (Farben, Schriften, Geometrie) plus optionaler Skin und Hintergrund. Fünf Themes und zehn Hintergründe sind enthalten; ein neuer Hintergrund ist eine Datei in `public/backgrounds/` und ein Eintrag in `themes.ts`. Die Oberfläche ist in sechs Sprachen übersetzt (EN, BG, DE, RU, TR, Romani); „Standard Galactic“ ist ein Schrifttausch über den englischen Texten, keine Übersetzung, und sechs Scherzmodi sind Kodierer über den englischen Texten — insgesamt dreizehn Einträge in der Auswahl. `npm run typecheck` listet fehlende Einträge.

## Was sie nicht tut

Keine Anwendungen bauen, keine Spezifikationen reparieren, keine Prüfungen nachbauen. Fehlt etwas in der GUI, gehört es in die CLI.
