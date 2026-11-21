# Das Problem und das Versprechen

**Genesis** erzeugt vollständige ASP.NET-Core-10-MVC-Anwendungen aus zwei JSON-Dateien. Das Ergebnis ist kein Gerüst. Eine erzeugte Anwendung hat:

- **Konten**: Registrierung, Anmeldung, Abmeldung, Passwortwechsel, E-Mail-Wechsel, Profilverwaltung;
- **Autorisierung**: Rollen, einen rollenbezogenen Admin- und einen Client-Bereich, Eigentumsregeln pro Datensatz und einen serverseitigen Schutz, der einen Administrator am Löschen des eigenen Kontos hindert;
- **Inhalte**: Modelle mit Validierung und bulgarischen Bezeichnungen — Beiträge mit Kommentaren und Stimmen, Produkte, Kategorien, Kataloge, Rezensionen, Follows und was die Spezifikation sonst deklariert;
- **Handel**, wenn aktiviert: Warenkorb, Bestellung, Bestellpositionen, Historie;
- **Medien**: Galerien mit mehreren Bildern, Hochladen, Ersetzen, Löschen, lokaler Cache und mitgelieferte Ersatzbilder;
- **Seiten**: Startseite, statische Kontaktseite, Kartenraster mit Suche und Sortierung, paginierte Admin-Tabellen und eine zusammengefasste Seite „Spezifikationen“ für kleine Modelle;
- **Darstellung**: responsives Bootstrap-Layout, Theme und Design-Preset pro Projekt;
- **Daten**: eine Datenbank, die beim ersten Start bereits benutzt aussieht — Eigentümer, Preise, Daten, Kommentare, Bestellungen, Bilder.

Dann wird es geprüft: Die Anwendung wird gebaut, ihre Datenbank migriert, sie wird gestartet, über das echte Formular angemeldet, die Routen, die die Spezifikation deklariert, werden abgerufen, und die abgerufenen Seiten werden auf englische Reste durchsucht.

## Das Problem, für das es gebaut wurde

Ein Diplom- oder Kursprojekt wird an *Kohärenz* gemessen: Der Autor muss jede Tabelle, jeden Bildschirm und jede Zugriffsregel erklären können, und die Anwendung muss auf dem Rechner des Prüfers laufen. Handarbeit kostet Wochen und hinterlässt Lücken — ein vergessenes `[Authorize]`, eine englische Beschriftung in bulgarischer Oberfläche, eine leere Datenbank, die die Anwendung kaputt aussehen lässt, eine Anmeldeseite, die auf einem frischen Klon scheitert.

Genesis macht daraus eine **deklarative Eingabe** und eine **mechanische Garantie**: Sie beschreiben die Anwendung, Sie beschreiben die Daten, der Generator erzeugt die Anwendung und **prüft** sie anschließend: Er baut sie, migriert ihre Datenbank, startet sie, meldet sich über das echte Formular an, ruft die Routen ab, die die Spezifikation deklariert, und durchsucht die abgerufenen Seiten auf englische Reste.

Das Ergebnis ist kein Gerüst, das auf Fertigstellung wartet. Es ist eine gewöhnliche MVC-Anwendung, wie sie eine kompetente Studierende schreiben würde — vollständig genug, um sie vorzuführen und zu verteidigen.

### Ein Verkäufer, viele Projekte

Ein Projektgenerator wird selten einmal von einer Person benutzt. Jemand baut ein Projekt für einen Freund, dessen Kommilitonen wollen ihre eigenen, und in einem Semester liefert derselbe Entwickler acht oder zehn Projekte an dieselbe Schule — manchmal an dieselbe Klasse, in der alle Arbeiten an einem Nachmittag gelesen werden.

Daraus folgt eine Anforderung: Teilen alle gelieferten Anwendungen Markup, CSS, Variablennamen und Seed-Zeilen, bricht die Arbeit zusammen. Zwei Arbeiten nebeneinander sind offensichtlich eine Quelle, und beide Studierenden sind exponiert. Vielfalt ist deshalb Teil des Produkts, nicht Dekoration.

Jedes Projekt löst sein eigenes Profil aus seinem Seed:

| Was variiert | Beispiele |
|---|---|
| Codestil | Initialisierung von Sammlungen, Namespace-Form, DI-Registrierung, String-Aufbau, Kommentardichte |
| Namen | Namen der Rollen und jede aus der Spezifikation stammende Bezeichnung |
| Darstellung | Bootstrap-Theme, Navbar, Buttonform, Design-Preset (Akzent, Neutraltöne, Schriften, Tiefe) |
| Inhalt | Zeilen, Eigentümer, Preise, Daten, Rezensionstexte, abgeleitete Passwörter |
| Zusammensetzung | welche Funktionen aktiv sind und damit welche Seiten und Tabellen existieren |

Die Vielfalt ist deterministisch: gleicher Seed ergibt dieselbe Anwendung, anderer Seed eine sichtbar andere. Es geht nicht um Zufälligkeit, sondern darum, dass zwei Arbeiten aus derselben Hand nicht als Kopien erkennbar sind.

Die ehrliche Grenze: Die Seiten*struktur* stammt aus einem Satz Vorlagen. Anwendungen unterscheiden sich in Stil, Namen, Inhalt und Funktionsumfang — nicht in der Form einer CRUD-Tabelle oder des Kartenrasters. Strukturell andere Anwendungen bräuchten Layout-Varianten, die es noch nicht gibt ([Kapitel 22](22-limits.md)).

## Die zwei Eingaben

| | `project-spec.json` | Seed-JSON |
|---|---|---|
| Beantwortet | *Was ist die Anwendung?* | *Was ist darin?* |
| Enthält | Modelle, Eigenschaften und `displayName`, Beziehungen, Validierungen, DTOs, Identität, Bereiche, Controller samt Herkunft, Views und Seitentypen, Funktionen, Zeilenzahlen | je ein Array pro Modell, `refKey` / `parentRef_*`, `$seedUser:N`, Bildsuchbegriffe |
| Ändert sich, wenn | die Domäne sich ändert | die Demodaten sich ändern |
| Validiert durch | `spec.schema.json` + semantische Regeln | ein aus dieser Spezifikation **abgeleitetes** Schema |
| Details | [Kapitel 11](11-contracts.md) | [Kapitel 11](11-contracts.md), Beispiel in [Kapitel 6](06-spec-to-seed.md) |

Die Trennung hat echte Arbeit: Ein Seed gilt nur für die Modelle, für die er geschrieben wurde; eine Änderung der Spezifikation entwertet ihn still und die Stufe erzeugt ihn neu. Form und Inhalt in getrennten Dateien sind unabhängig prüfbar, ein Mensch kann beide schreiben, und die Pipeline läuft ohne Netz.

## Das Versprechen, genau gesagt

„Im Umfang“ heißt, dass eine Stufe es tatsächlich prüft:

| Eigenschaft | Durchsetzung |
|---|---|
| Kompiliert | jede codeerzeugende Stufe endet mit `dotnet build` |
| Migriert | `ef migrations add` + `ef database update` in `seed`, erneut in `verify` |
| Startet | `verify` startet Kestrel auf einem freien Port |
| Startseite funktioniert | `GET /` ist Pflicht und wird nie übersprungen |
| Der Admin kann sich anmelden | echte Formularanmeldung als geseedeter Admin; eine gescheiterte Anmeldung lässt `verify` scheitern |
| Deklarierte Seiten werden erreicht | jede `cardGrid`-Seite anonym, dann jede `adminTable` und jede Funktionsroute als Admin; jede deklarierte Seite, die nicht mit 200 antwortet, lässt `verify` scheitern |
| Geschützte Seiten sind geschützt | jede Admin-Seite und jede Funktionsseite mit Anmeldepflicht wird anonym abgerufen und muss mit 401/403 antworten oder zur Anmeldung umleiten |
| Oberfläche ist bulgarisch | Sichttext-Scan über jede Seite, die mit 200 geantwortet hat: 2+ lateinische Wörter, davon mindestens eines ein gängiges englisches Wort (reine Markennamen lösen nichts aus) |
| Zugesagte Dateien existieren | Manifestprüfung vor dem Start |

Was es **nicht** verspricht — dass ein angemeldeter Benutzer von den Datensätzen *anderer* Benutzer ferngehalten wird (Eigentumsregeln stammen aus den Abweichungen der Spezifikation, nicht aus einer Prüfung), Verhalten jenseits der deklarierten Routen, Designbewertung, Performance, fachliche Korrektheit jenseits der DB-Constraints — steht in [Kapitel 22](22-limits.md). Lesen Sie das, bevor Sie das Werkzeug jemandem versprechen.

## Woher der Name kommt

Genesis ist der englische Name; die bulgarische Ausgabe trägt einen eigenen bulgarischen Titel. Die ausführbare Datei heißt `dps-nn`, die Quellordner heißen `src/Genesis.*`, die Assemblies und Namespaces `GenesisV6.*`, und dabei bleibt es. Eine Umbenennung würde jede erzeugte Datei, jede Fixture und jedes historische Log brechen — die Grenze ist in ADR-009 festgehalten.
