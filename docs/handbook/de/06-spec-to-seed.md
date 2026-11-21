# Von der Spezifikation zu Seed-Daten

Eine erzeugte Anwendung, die mit leeren Tabellen startet, ist keine Demo, sondern eine Datenbank. Der Seed existiert, damit der erste Start wie ein benutztes Produkt aussieht: Zeilen in jeder Liste, Bilder auf jeder Karte, Bestellhistorie, Kommentare und Stimmen, Nutzer, die etwas besitzen.

## Der Vertrag auf einem Bildschirm

```json
{
  "Brand":   [ { "refKey": "brand-1", "Name": "Samsung" } ],
  "Product": [ {
      "refKey": "prod-1",
      "parentRef_Brand": "brand-1",
      "parentRef_Category": "cat-1",
      "Title": "Samsung Galaxy A54 128GB Smartphone",
      "Price": 520.0,
      "Quantity": 1,
      "UserId": "$seedUser:1",
      "imageKeywords": ["samsung galaxy smartphone front view",
                        "samsung galaxy smartphone side view"]
  } ],
  "OrderItem": [ { "parentRef_Order": "order-1", "parentRef_Product": "prod-1",
                   "Quantity": 1, "UnitPrice": 520.0 } ]
}
```

| Feld | Pflicht, wenn | Regel |
|---|---|---|
| Modellschlüssel | das Modell hat `seeding.rowsPerModel` | ein Array, **mindestens** so viele Zeilen |
| `refKey` | das Modell ist Elternteil | eindeutig, kebab-case, lateinisch |
| `parentRef_<Target>` | es gibt eine `manyToOne`-Beziehung | muss auf einen `refKey` derselben Datei zeigen |
| deklarierte Eigenschaft | außer Schlüssel, Fremdschlüssel, nullable | Typ der Eigenschaft; Daten ISO-8601, Geld als Zahl |
| Feld `…UserId` | die Spezifikation deklariert es | `"$seedUser:0"`, `:1`, … in Reihenfolge der `seedUsers` |
| `imageKeywords` | das Modell hat einen `images`-Block | Array: genau **1** bei `single`, **2–4** bei `multiple` |

Objekte sind geschlossen: ein zusätzliches Feld ist ein Fehler. Keine ID wird geschrieben — die Datenbank vergibt Schlüssel, `SeedData.cs` merkt sich `refKey` → echter Schlüssel.

## Warum symbolische Verweise

Das Modell kann nicht wissen, welche Identität SQL Server vergibt, und ein Mensch kann Nummern über Läufe hinweg nicht synchron halten. Also verweisen Zeilen auf Namen. `Data/SeedData.cs` fügt Modelle in **topologischer Reihenfolge** ein, merkt sich die echten Schlüssel und verbindet dann die Kinder. Deshalb kann ein Modell gleichzeitig Kind (Product → Brand) und Elternteil (Product ← OrderItem) sein.

## Wo die Datei liegt

| Pfad | Rolle |
|---|---|
| `prompts/in/seed-response.json` | die kanonische Übergabe — was `seed` liest |
| `seeds/*.json` | mitgelieferte Kandidaten; die Auswahl wird dorthin kopiert |
| `prompts/out/seeds/<project>-seed.json` | Archiv des tatsächlich Verwendeten |
| `<out>/<Project>/Data/seed-data.json` | dieselben Daten **in** der Anwendung (beim Start gelesen) |

## Von Hand schreiben

Das Repository enthält einen portablen Skill (`utils/claude-skills/dps-nn-seed-authoring`), der den Prompt der Stufe nachbildet, damit ein Chat-Client die Datei ohne Lauf erzeugen kann. Zwei Eigenheiten: Er fragt nach der Zahl der Seed-Nutzer, bevor er schreibt, und bleibt knapp — Seed-Erzeugung ist mechanisch, sobald die Spezifikation steht.

Die Inhaltsregeln:

1. Bulgarisch für jeden lesbaren Wert bei `meta.language = bg`; Markennamen bleiben.
2. Echte Städte, glaubwürdige Preise und Daten — kein „Test 1“, kein Lorem ipsum.
3. Keine zwei Zeilen mit derselben beschreibenden Zeichenkette.
4. `seedHint` beachten, wenn vorhanden.
5. Daten ISO-8601; Dezimalzahlen als Zahlen.
6. Eigentum über die Nutzer verteilen.
7. **Szenarioabdeckung**: Kataloge mit Artikeln, ein nicht leerer Warenkorb, vergangene Bestellungen, ein gegenseitiges Follow, Rezensionen, die sich an verschiedene Benutzer richten, verteilte Kommentare und Stimmen.

Regel 7 trennt die Demo von einer ausgeschütteten Tabelle und wird am häufigsten übersprungen.

## Bilder

Jede Zeile eines bildtragenden Modells trägt `imageKeywords`. Die Stufe lädt eine Datei pro Phrase, benennt sie `{model}-{row}-{img}.jpg` und cacht in `.image-cache/`, damit eine Neuerzeugung keine Anfragen kostet. Anbieter: Pexels, Pixabay, Picsum ohne Schlüssel oder ein lokaler Ordner via `--image-pack`. Die Anwendung liefert aus `wwwroot/images/…` und verlinkt nie extern. Mitgelieferte Ersatzbilder decken einen Fehlschlag ab.

## Validierung, Wiederverwendung, typische Fehler

Die Stufe baut ein JSON-Schema aus der aktuellen Spezifikation. Zwei Folgen: Ein gültiges `Data/seed-data.json` wird **wiederverwendet** und der Austausch übersprungen; eine Modelländerung entwertet den alten Seed und er erzeugt sich neu.

Die Meldungen des Validators stammen von der JSON-Schema-Bibliothek selbst, mit dem Pfad der fehlgeschlagenen Zeile als Präfix; die Tabelle beschreibt den Fehler, statt ihn wörtlich zu zitieren.

| Fehler (was die Meldung sagt) | Ursache | Behebung |
|---|---|---|
| eine Pflichteigenschaft `parentRef_Brand` fehlt | das Modell ist ein Kind, aber die Zeile hat keinen Elternverweis | `refKey` aus derselben Datei eintragen |
| eine Pflichteigenschaft `refKey` fehlt | das Modell ist Elternteil (ein anderes Modell zeigt darauf) | jeder Zeile einen eindeutigen `refKey` geben |
| `Value is "string" but should be "array"` bei `imageKeywords`, oder falsche Zahl | einzelne Zeichenkette, oder 3 Phrasen bei `single` | 1 Phrase bei `single`, 2–4 bei `multiple` |
| `All values fail against the false schema` | ein Feld, das nicht im Modell steht (geschlossene Objekte lehnen unbekannte Schlüssel auf diese Weise ab) | entfernen — reine Inhaltsnotizen gehören in `seedHint` oder nirgendwohin |
| zu wenige Zeilen | unter `rowsPerModel` | Zeilen ergänzen oder Zahl senken |
| `verify` findet Englisch | englische Beschreibung in den Daten | bulgarischer Inhalt (Keywords sind die Ausnahme) |

## Nach dem Einlesen

Die Anwendung lädt sich beim Start selbst, geschützt durch eine Prüfung „schon befüllt?“ am ersten Modell, sodass ein Neustart keine Zeilen dupliziert. Zum bewussten Neuladen: Datenbank löschen (oder umbenennen) und erneut starten — `SeedStage` merkt sich in `logs/<Project>/db.json`, welche Datenbank migriert wurde.
