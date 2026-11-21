# Qualitätsarchitektur

Qualität ist hier keine Phase am Ende, sondern eine Menge von Gates über die Pipeline verteilt, plus eine Testpolitik, die festlegt, was ein behobener Fehler hinterlässt.

## Vier Arten von Gates

| Gate | Ort | Verweigert |
|---|---|---|
| Schema + Semantik | `spec-expand` (und jedes `SpecLoader.Load`) | eine Spezifikation, die nichts bedeuten kann |
| Baugate | Ende von `scaffold`, `emit-models`, `identity`, `emit-controllers-views`; in `seed` | Code, der nie kompiliert wurde |
| Migrationsgate | in `seed`, erneut in `verify` | eine Modelländerung, die keine Datenbank erreicht |
| `verify` | Stufe 8 | eine Anwendung, die baut, aber nicht startet, anmeldet, rendert oder bulgarisch ist |

Die Reihenfolge innerhalb der Gates ist billig-zu-teuer: Schema vor Erzeugung, Manifest vor Start, `GET /` vor den deklarierten Routen, die Routen vor dem Sprachscan.

## Was Unit-Tests abdecken

Die .NET-Suite testet **Entscheidungen**, nicht Artefakte: die Agentenschleife (Erfolg, eine Reparatur, Umgebungsstopp, erschöpftes Budget, Entwurfsfehler, wartender Austausch) mit Fälschungen; die semantischen Regeln; Vielfalt (gleicher Seed gleiches Ergebnis, Passwörter konform); Fortsetzen und Log; Ableitung des Seed-Schemas; `doctor`; die Englisch-Heuristik.

Die GUI hat ihre eigene `node --test`-Suite für die reine Logik: Themes, Vollständigkeit der i18n-Tabellen, Parsing des Abschlussbanners, die Zustandsmaschine der Laufsteuerung, letzte Projekte, Hilfsfunktionen des Agenten und der Parser des Musikordners.

Die tatsächliche Erzeugung wird nicht unit-getestet — dafür ist der Lauf mit `verify` da. `dotnet build` zu mocken würde den Mock testen.

## Die Regel für jeden Fehler: rot, grün, zurücknehmen, grün

1. **Rot** — ein Test reproduziert den Fehler und scheitert aus dem genannten Grund.
2. **Grün** — die Reparatur, bis er besteht; nichts anderes ändern.
3. **Zurücknehmen** — die Reparatur rückgängig machen, den Test behalten, ihn erneut scheitern sehen. Ein Test, der ohne die Reparatur besteht, hat den Fehler nie geprüft.
4. **Grün** — die Reparatur zurückbringen; Test und Reparatur gemeinsam committen.

Der Schritt des Zurücknehmens ist der entscheidende, weil er der einzige Beleg ist, dass der Test mit dem Fehler zusammenhängt. Er kostet ein Kommando; oft genug übersprungen, bedeutet die Suite nichts mehr.

## Neues Verhalten: erst der Test

Neue Funktionen folgen derselben Reihenfolge. Der Test, der das Verhalten beschreibt, wird zuerst geschrieben und scheitern gesehen, der Code lässt ihn bestehen, und danach wird der Code mit dem Test als Sicherheitsnetz aufgeräumt. Ein Test, der nach dem Code geschrieben wird, beschreibt tendenziell, was der Code tut, nicht was er tun soll.

## Regression: die Suite wächst nur

- Jeder behobene Fehler hinterlässt seinen Test. Ein Regressionstest wird nie gelöscht, übersprungen oder abgeschwächt, damit ein Build durchläuft — scheitert er, ist der Fehler zurück.
- Beide Suiten laufen bei jedem Push und Pull Request; eine Änderung mit roter CI wird nicht gemergt.
- **Ein instabiler Test ist ein Fehler**, im Test oder im Code, und bekommt dieselbe Behandlung aus Rot, Grün und Zurücknehmen. Der Test der parallelen Ausgabe, der nur auf einer einzigen CPU scheiterte, wurde absichtlich reproduziert (`taskset -c 0`, 30 Fehlschläge in 30 Läufen), behoben, indem die Testelemente warten, bis eine zweite Worker-Bahn gestartet ist, und erneut ausgeführt (0 in 30).
- Für alles, was erzeugte Anwendungen ändert, ist der End-to-End-Test die Pipeline selbst: Ein vollständiger Lauf auf der FaceNote-Fixture muss `verify` bestehen.

## Mutanten: die Tests testen

Bestandene Tests zeigen, dass der Code für die getesteten Fälle funktioniert; sie zeigen nicht, dass die Tests einen Fehler bemerken würden. **Mutationstests** tun das: Sie nehmen kleine absichtliche Änderungen am Code vor — kehren eine Bedingung um, entfernen einen Aufruf, ändern eine Konstante — und lassen die Suite gegen jede geänderte Kopie laufen, einen *Mutanten*. Ein Mutant, bei dem die Suite scheitert, ist *getötet*; einer, der alle Tests besteht, *überlebt* und markiert Verhalten, das kein Test tatsächlich prüft.

- Überlebende Mutanten in wichtigem Code (Stufenlogik, Spezifikationsvalidierung, die Reparaturschleife des Agenten, Pfadbehandlung) bekommen einen Test, der sie tötet.
- Der Schritt des Zurücknehmens oben ist ein handgemachter, gezielter Mutant: Die Reparatur zu entfernen ist die wichtigste Mutation überhaupt.
- Die Werkzeuge sind **Stryker.NET** für die .NET-Suite und **StrykerJS** für die GUI. Sie sind **noch nicht in das Repository oder die CI eingebunden** — ein vollständiger Lauf ist langsam —, daher werden Mutationsläufe bei Bedarf pro Modul gemacht, vor einem Release oder nach einer großen Änderung an diesem Modul.

Die vollständige Politik, einschließlich dessen, was „fertig“ für eine Änderung heißt, steht in `docs/explanation/testing-policy.md`.

## Kontinuierliche Integration

| Job | Läuft auf | Beweist |
|---|---|---|
| build-and-test | Windows, jeder Push/PR zu master | `dotnet build` + `dotnet test` |
| gui-test | Windows | Typecheck, Build aller drei Ziele, `node --test` |
| `handbook.yml` | Doku-Änderungen, Releases, manuell | dieses Buch auf Englisch (`scripts/build-handbook2.py --lang en`) baut; bei einem Release wird es angehängt. Die Übersetzungen werden lokal gebaut |
| release | Tags | der Windows-Installer entsteht |
| lite-mirror | master und Tags | die musikfreie Ausgabe wird gebaut |

Windows ist kein Zufall: Der Installer ist NSIS, und der Release-Job baut ihn auf demselben Runner-Image, die CI läuft also dort, wo das Produkt ausgeliefert wird. Die CI führt **keine** vollständige Erzeugung aus — dafür braucht es SQL Server LocalDB und mehrere Minuten Builds, und sie läuft lokal mit den Befehlen unten. Der Generator selbst baut und besteht seine Unit-Tests auch unter Linux; nur die Erzeugung braucht Windows.

## Warum „fertig“ ein Pipeline-Lauf ist

```bash
dotnet build
dotnet test
cp seeds/facenote-rich-seed.json prompts/in/seed-response.json
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

Besteht `verify`, ist die Änderung gegen eine echte Datenbank, einen echten Server und die echten Routen bewiesen.

## Das Wissen, das nicht im Code steht

Drei laufende Aufzeichnungen tragen Qualität über Sitzungen hinweg: `changelogs/` (ein Eintrag pro Änderung), `plan/decisions.md` (jeder Fehler und **jede bereits versuchte und verworfene Theorie**) und `plan/state.md` (Sitzungsprotokoll). Die Regel, die sie bezahlt: Vor einem Reparaturvorschlag für ein wiederkehrendes Symptom die Aufzeichnung lesen. Nach zwei oder drei Fehlversuchen ist der nächste Schritt Instrumentierung, nicht ein vierter Verdacht.
