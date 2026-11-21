# Der erste Lauf

**Nötig:** Windows mit .NET 10 SDK, SQL Server Express LocalDB, `dotnet-ef` 10.x und `dotnet-aspnet-codegenerator`. Auf Linux baut und testet sich der Generator, aber die Datenbank der erzeugten Anwendung läuft nicht — der Lauf stoppt erwartungsgemäß beim ersten `ef`-Aufruf.

Prüfen Sie die Maschine, bevor Sie das Werkzeug beschuldigen:

```bash
dotnet run --project src/Genesis.Cli -- doctor
```

`doctor` druckt je Voraussetzung eine Zeile mit dem genauen Reparaturbefehl. Die GUI führt ihn bei jedem Start aus und zeigt den Bericht in ihrem Debug-Terminal.

## Bauen und die Fixture erzeugen

```bash
dotnet build
dotnet test
mkdir -p prompts/in
cp seeds/facenote-rich-seed.json prompts/in/seed-response.json
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

Warum die Kopie: Die Stufe `seed` fragt ein Modell nach Daten, solange keine Antwort existiert. Liegt der mitgelieferte Seed dort, wo eine Antwort läge, läuft alles **offline und ohne Schlüssel** — manueller und automatischer Weg sind derselbe Dateivertrag. Bilder werden beim ersten Lauf geladen und danach aus dem Cache bedient.

Beobachten Sie die Konsole. Jede Stufe kündigt sich an (`=== stage: seed ===`), und der Lauf endet mit einer Zeittabelle und einem Banner (auf der Konsole in einem Kasten gezeichnet; die Rollennamen variieren je Projekt):

```
FaceNote
  http://localhost:5xxx
  Admin            admin@facenote.bg / <derived password>
  Client           user@facenote.bg / <derived password>
(4 more seed user(s) - full list: logs/FaceNote/seed-credentials.txt)
Database: <database name>
Start: dotnet run --project "out/FaceNote"
```

Sofern die Spezifikation kein Passwort setzt, ist das Passwort jedes Seed-Nutzers **abgeleitet**: aus dem Variation-Seed des Projekts und der E-Mail des Nutzers. Dieselbe Spezifikation druckt daher immer dieselben Zugangsdaten, und zwei Projekte teilen nie ein Passwort. Das Banner zeigt einen Admin und einen gewöhnlichen Nutzer; die vollständige Liste steht in der Datei mit den Zugangsdaten, die es nennt.

## Wenn Code 3 kommt

Code `3` ist kein Fehler — ein LLM-Austausch wartet auf eine Datei. Die Anfrage liegt bereits; tragen Sie die Antwort in `prompts/in/<stage>-response.json` ein oder setzen Sie `ANTHROPIC_API_KEY`, dann mit `--resume` erneut starten. Einige Schritte stellen eine Frage im Terminal: `spec build` fragt immer nach den Zeitstempel-Konventionen (`conventions.timestamps.createdAt? [y/n]`), und Auswahldialoge erscheinen, wenn mehrere Seed-Dateien infrage kommen; `--no-interactive` macht aus jeder solchen Frage einen Fehler statt einer Eingabeaufforderung.

## Was Sie ansehen sollten

| Pfad | Warum |
|---|---|
| `out/FaceNote/` | die Anwendung; `dotnet run` darin und mit den Bannerdaten anmelden |
| `out/FaceNote/dps-nn.log.jsonl` | Stufengrenzen und Zeiten — was die GUI verfolgt |
| `out/FaceNote/plan/state.json` | abgeschlossene Stufen; Löschen erzwingt einen sauberen Lauf |
| `wwwroot/output/verify-language-report.txt` | Urteil der Sprachprüfung, bestanden **oder** übersprungen |
| `logs/registry.json` | welches Projekt aus welcher Spezifikation und welchem Seed entstand |
| `prompts/out/schemas/`, `prompts/out/seeds/` | dauerhaftes Archiv des Verwendeten |

## Wiederholen und was „Fortsetzen“ heißt

`run --all` überspringt Stufen, die in `state.json` stehen. Nach einem Absturz ist das gewollt, nach einer Template-Änderung meist nicht; dann erzwingen Sie den geänderten Teil:

```bash
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --from emit-controllers-views
```

`--from` startet diese Stufe und alle folgenden neu. `spec-expand` läuft bei jedem Aufruf zuerst, sofern sein eigener Eintrag nicht „fertig“ sagt; `spec-build` ist nicht Teil der Schleife ([Kapitel 9](09-workflow.md)).

## Der schnellste absichtliche Bruch

Einmal tun; lehrt mehr als jedes Kapitel:

1. In `out/FaceNote/Areas/Client/Views/Products/Index.cshtml` eine bulgarische Beschriftung durch Englisch ersetzen.
2. Mit `--from verify` neu starten.
3. Den Fehler lesen: Der Sprachbericht nennt Seite und Textstelle.

Dann die Änderung zurücknehmen und dasselbe Kommando erneut laufen lassen: Es besteht wieder. Achten Sie darauf, wo Sie die Reparatur vornehmen mussten: Das Ergebnis ist erzeugt, daher gehört die Korrektur bei einer echten Änderung nach `templates/`, niemals nach `out/`.
