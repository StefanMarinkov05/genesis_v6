# Die Seite „Spezifikationen“

Gekürzt aus dem vollständigen Kapitel der englischen Ausgabe (ADR-012).

## Das Problem

Jedes Modell bekommt standardmäßig einen eigenen Administrationseintrag. Bei vierzehn Modellen wird das Menü unlesbar, und die Hälfte der Einträge führt zu einer Tabelle mit zwei Spalten.

## Die Lösung

| Flag | Wann | Verhalten |
|---|---|---|
| `isSpecification` | atomares Referenzmodell ohne ausgehende Beziehung (Brand, Category) | Tab mit vollem CRUD |
| `isLookup` | Verknüpfungszeile (CartItem, OrderItem, PostComment) | Tab nur lesend, mit automatischen Filtern |
| kein Flag | echtes Inhaltsmodell | eigene Admin-Seite |

Die Seite ist eine, mit Tabs. Die Flags werden auf den injizierten Controller kopiert, damit das Verhalten überall gleich ist.

## Die Filter

Jede `manyToOne`-Beziehung des Verknüpfungsmodells wird ein Auswahlfeld; jedes auf `UserId` endende Feld wird eine Nutzerauswahl. Die Filterleiste ist ein gemeinsames Partial, das auch andere Admin-Seiten nutzen.

## Warum es besser ist

Die Navigation bleibt lesbar, und die kleinen Modelle verlieren kein CRUD — es liegt nur auf einer gemeinsamen Seite. Der Preis sind zwei Flags, die nach Regel gesetzt werden müssen, und eine Seite, die beiden Modi treu bleiben muss.
