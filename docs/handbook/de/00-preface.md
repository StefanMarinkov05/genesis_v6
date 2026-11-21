<!-- front-matter -->
# Vorwort — wie dieses Buch zu lesen ist

Dieses Buch beschreibt ein System von Anfang bis Ende: **Genesis** (`dps-nn` auf der Kommandozeile), einen Generator, der aus zwei JSON-Dateien eine vollständige, mit Daten gefüllte und geprüfte ASP.NET-Core-Anwendung erzeugt.

Das Buch geht von der Breite in die Tiefe: Zuerst muss die Form des Systems sichtbar sein, bevor die Teile Sinn ergeben. Jeder Teil setzt nur die vorherigen voraus.

## Die Leiter

| Teil | Kapitel | Sie lernen |
|---|---|---|
| **I. Überblick** | 1–3 | welches Problem gelöst wird, eine Erzeugung als Geschichte, und wie die fertige Anwendung für ihren Nutzer aussieht. Ohne Interna. |
| **II. Benutzung** | 4–7 | starten, Spezifikation aus einer Aufgabenstellung, Seed-Daten, Agentenmodus. |
| **III. Innenleben** | 8–12 | die Architektur in einem Bild: Stil, Muster, Konventionen, Werkzeuge; dann die Pipeline, das Ergebnis, die zwei Verträge, die Prinzipien. |
| **IV. Architektur im Detail** | 13–16 | Komponenten und Abhängigkeitsregeln; wie Qualität erzwungen wird; wie das Projekt gebaut wird; wie es läuft und ausgeliefert wird. |
| **V. Vertiefungen** | 17–21 | die langen technischen Kapitel, die die Erzählung nur zusammenfasst. |
| **VI. Referenz** | 22–25 | was es nicht kann, was ein Käufer erhält, Glossar, Entscheidungsindex. |

Lesen Sie Teil I auch dann, wenn Sie das Werkzeug nur benutzen wollen — die Begriffe kommen von dort. Lesen Sie Teil VI, bevor Sie das Werkzeug jemandem versprechen.

## Für wen welcher Teil ist

| Leser | Liest |
|---|---|
| Prüfer, Dozent, Arbeitgeber | Teile I und VI, dann Kapitel 8 für die Architektur und Kapitel 9 für die Mechanik |
| Studierende, die ein Projekt bauen | Teile I–II, mit Kapitel 22 daneben |
| Mitwirkende oder Agenten | Teile I, III, IV, dann die Dateikarte und die ADRs |

## Konventionen

- Codeblöcke tragen eine Sprachmarke (`SHELL`, `JSON`, `C#`, …). Ein Block ohne Marke ist beispielhafte Ausgabe, kein Befehl.
- Pfade sind relativ zur Repository-Wurzel, sofern sie nicht mit `/` oder einem Laufwerksbuchstaben beginnen.
- **Erzeugt** heißt „in `out/` entstanden“; **geprüft** heißt, dass die Stufe `verify` bestanden wurde — niemals „es kompiliert“.
- Diagramme sind Mermaid-Quelltext und werden als Vektorfiguren gesetzt.
- ADRs werden mit Nummer zitiert (`ADR-004`) und in [Kapitel 25](25-decisions.md) indexiert.

## Status dieser Ausgabe

**Ausgabe 2, deutsch.** Übersetzung der freigegebenen englischen Ausgabe; Aufbau, Abschnitte und Kapitelnummerierung entsprechen ihr. Die Übersetzung ist zum Lesen geschrieben, nicht Zeile für Zeile: der Inhalt ist derselbe, der Text aber straffer als der englische. Die technischen Kapitel in Teil V sind in gekürzter Form übersetzt — die vollständigen Originale stehen in der englischen Ausgabe.

Die Ausgabe folgt einer Prüfung der ersten Ausgabe gegen den Code und einer zweiten Prüfung der Aussagen dieser Ausgabe gegen den Code; die Korrekturen sind im Changelog des Repositorys festgehalten (Einträge 137–139), die bei der Prüfung gefundenen Lücken stehen in [Kapitel 22](22-limits.md). Diese Ausgabe ist übernommen worden; die Dokumente, die sie ersetzt, sollten daher außer Dienst gestellt werden, statt parallel weitergeführt zu werden: Zwei Beschreibungen desselben Systems laufen auseinander, und die Kopie, die niemand liest, ist die, die am Ende falsch ist.

Das Produkt heißt **Genesis** auf Englisch und in dieser Ausgabe; die bulgarische Ausgabe trägt einen eigenen bulgarischen Titel. Die ausführbare Datei heißt in beiden `dps-nn`, und die internen Assembly-Namen bleiben `Genesis.*`.
