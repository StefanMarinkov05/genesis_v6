# Die zwei Verträge

Spezifikation und Seed sind die **öffentliche Schnittstelle** des Systems. Alles innerhalb der Pipeline ist Detail, das sich ändern darf; diese beiden Formen sind das, wogegen Code (und das Modell) geschrieben wird.

## Vertrag 1: die Spezifikation

| Abschnitt | Konsument | Garantie |
|---|---|---|
| `meta` | jede Stufe | Identität, Sprache, Datenbank, Stil, Routen, `unsupportedRequests` |
| `conventions` | `emit-models`, `identity` | Zeitstempel und Soft Delete für das ganze Projekt |
| `variationProfile` | `spec-expand` | optionale Pins über einem deterministischen Profil |
| `models` | alles Folgende | Entitäten, Spalten, Bezeichnungen, Validierungen, Beziehungen |
| `dtos` | `emit-models`, `emit-controllers-views` | zusammengesetzte Lesemodelle (verdrahtet, unerprobt) |
| `customValidationAttributes` | `emit-models` | Datumsregeln als echte C#-Attribute |
| `identity` | `identity` | Rollen, Seed-Nutzer, Erweiterungen, Seiten, Texte |
| `areas` | `identity`, `emit-controllers-views` | Abbildung Bereich → Rolle |
| `controllers` | `emit-controllers-views` | welche Controller existieren und wie sie entstehen |
| `views` | `emit-controllers-views`, `verify` | Layout und Seitentypen |
| `features` | `spec-expand` | welche Fragmente einfließen |
| `seeding` | `seed` | Zeilenzahl, Bildquelle, Ausgabedatei |
| `outputReport` | `report` | welche Codepointer aufgelöst werden |

### Was das Expandieren tut

Die Spezifikation, die Sie schreiben, ist nicht die, die die Stufen lesen. `spec-expand` macht vier Dinge und schreibt `project-spec.lock.json`: eingeschaltete Funktionen mischen (mit Modellen, DTOs, Controllern und aufgelöstem `"$primaryModel"`); Administrations-CRUD für jedes Modell ohne solches injizieren; jedes `$variation:axis` ersetzen und Passwörter ableiten; das aufgelöste Profil zur Prüfbarkeit zurückschreiben. Expandieren ist deterministisch und idempotent: Wird dieselbe Spezifikation zweimal expandiert, entsteht dieselbe Lock-Datei.

Neben diesem Schritt steht ein optionaler: Ist ein API-Schlüssel gesetzt oder liegt bereits eine `expand-review`-Antwort bereit, prüft das LLM die Lock-Datei und schlägt übersehene Felder oder Beziehungen vor; jeder Vorschlag wird Ihnen als `[y/n]`-Frage gestellt, und ein angenommener wird nur **protokolliert**, damit Sie ihn von Hand übernehmen. Die Lock-Datei selbst wird vom Modell nie verändert.

### Zwei Validierungsschichten

| Schicht | Fängt | Beispiel |
|---|---|---|
| JSON Schema (`additionalProperties: false`) | Form, Enums, Typen, unbekannte Schlüssel | `generatedBy: "scaffoldd"` scheitert laut |
| Semantische Prüfungen | was ein Schema nicht ausdrückt | genau ein Schlüssel; Beziehungsziele existieren; Weiterleitungsziele existieren; Sortierfelder existieren; Bereichsrolle existiert; jede sichtbare Eigenschaft hat `displayName`; jeder Template-`Index` hat eine Seite |

Die zweite Schicht trägt die Meinungen des Projekts und liefert genau die Texte, die der Agentenmodus dem Modell zurückspielt.

## Vertrag 2: der Seed

Die Form wird **aus der Spezifikation abgeleitet**, daher gibt es keine eigene Schema-Datei. Die Aufgabe ist klein und streng: ein Array pro Modell mit `rowsPerModel`; Identität über Namen (`refKey`, `parentRef_<Target>`); Nutzer über Position (`$seedUser:N`); Bilder über Phrasen (`imageKeywords`, 1 bei `single`, 2–4 bei `multiple`); geschlossene Objekte; mindestens die konfigurierte Zeilenzahl.

Drei Folgen: Der Seed überlebt seine Spezifikation nicht; er ist Daten, kein Code (`Data/seed-data.json` wird beim Start gelesen); Mensch und Modell sind austauschbare Produzenten.

## Warum die Verträge Dateien sind und keine API-Aufrufe

Jeder Austausch mit dem Sprachmodell — Spezifikationsentwurf, Prüfung der Expansion, Seed, Reparatur im
Agentenmodus — ist ein Dateipaar aus Anfrage und Antwort unter `prompts/`. Diese Wahl gibt dem Projekt
vier Eigenschaften, auf die es angewiesen ist:

- **Läuft ohne Schlüssel** (Text in irgendeinen Chat einfügen), also auch in einer abgeschotteten Umgebung
  benutzbar.
- **Nachprüfbar:** der genaue Prompt und die genaue Antwort eines Laufs liegen auf der Platte, mit Hash
  im Protokoll.
- **Wiederholbar:** eine gespeicherte Antwort ist eine Testfixture; CI kann den Weg durch das Modell ohne
  Netz fahren.
- **Ein Prüfweg:** die Schemaprüfung ist für den manuellen und den automatischen Modus dieselbe.

Ein API-first-Entwurf hätte nichts davon, und eine Woche später könnte niemand die Frage beantworten,
warum genau diese Anwendung entstanden ist.
