# Rundgang durch das Ergebnis

Noch ohne Interna. Das sieht ein Mensch, wenn er eine erzeugte Anwendung öffnet. Beispiel ist die Fixture des Repositories — FaceNote, ein kleiner sozialer Marktplatz mit Produkten, Beiträgen, Katalogen, Warenkorb und Bestellungen.

## Anmelden

Die Anwendung startet mit `dotnet run` im erzeugten Ordner und zeigt eine Startseite mit dem Katalog. FaceNote legt sechs Konten an — zwei in der Admin-Rolle, vier in der Nutzerrolle. Das Banner am Ende des Laufs zeigt je eines davon; alle sechs mit ihren Passwörtern stehen in `logs/<Project>/seed-credentials.txt`.

| Konto | Rolle | Darf |
|---|---|---|
| Administrator | die Admin-Rolle des Projekts | alles: Katalog, Benutzer, Spezifikationen, Bilder |
| Leser | die Nutzerrolle des Projekts | stöbern, suchen, eigener Katalog, Warenkorb, Bestellungen, Profil |

Anmeldung, Registrierung, Abmeldung und Kontoverwaltung sind bulgarisch und im Stil der Seite. Nirgends gibt es eine englische Ersatzseite — das ist der Punkt.

## Die Client-Seite

| Seite | Was sie ist |
|---|---|
| **Start** | Branding, empfohlene Inhalte, Links in den Katalog |
| **Kontakt** | statische Seite mit echtem Formular |
| **Kartenraster** | Karten mit Bildern und Preis; ein Suchfeld und eine Sortierung, beide auf dem Server angewendet |
| **Details** | ein Datensatz mit Beziehungen, Galerie und Funktionsaktionen |
| **Funktionsseiten** | Warenkorb (mit Bestellung und Historie, wenn `orders` aktiv ist), persönliche Kataloge, Beitragsstrom mit Kommentaren und Stimmen, öffentliche Profile, Rezensionen, Follows — jede nur, wenn die Funktion aktiv ist |
| **Profil** | eine öffentliche Seite pro Benutzer, die dessen Beiträge und Kataloge bündelt; wird erzeugt, wenn die Funktion `posts` oder `catalogues` aktiv ist, mit Avatar, wenn das Benutzermodell ein `ProfilePicturePath` hat. Mit `personalization` begrüßt die Navigationsleiste den angemeldeten Benutzer mit Namen, zeigt den Avatar und verlinkt „Mein Profil“ |

Suche und Sortierung sind beide Query-String-Parameter, die der Controller auf die Datenbankabfrage anwendet (die Suche läuft über die Textfelder des Modells). Beides ist erzeugt, nicht von Hand verdrahtet, und im Bericht als Codepointer vermerkt.

## Die Admin-Seite

| Seite | Was sie ist |
|---|---|
| **Dashboard-artige Navigation** | ein Eintrag pro echtem Modell plus eine Sammelseite für die kleinen |
| **Admin-Tabellen** | eine CRUD-Tabelle für jedes Modell, ob die Spezifikation es verlangt hat oder nicht; Sortierung und Paginierung gibt es dort, wo die Spezifikation sie konfiguriert |
| **Spezifikationen** | eine Seite: Tabs für kleine Referenzmodelle (volles CRUD) und für Verknüpfungsmodelle (nur lesen, automatisch gefiltert) |
| **Benutzer** | Benutzerverwaltung mit serverseitiger Regel gegen Selbstlöschung |
| **Bilder** | Bildverwaltung pro Modell: hochladen, ersetzen, löschen, lokal ausgeliefert |

Die Zusammenfassung zählt bei der Vorführung: Ein Projekt mit vierzehn Modellen hat ein lesbares Menü statt vierzehn fast gleicher Einträge.

## Die Daten, die schon da sind

Der Seed ist so geschrieben, dass jeder Bildschirm benutzt aussieht: Katalog mit Bildern und unterschiedlichen Preisen; Kataloge mit Artikeln; ein nicht leerer Warenkorb für mindestens eine Person; ein bis zwei vergangene Bestellungen mit Positionen; Beiträge mit Kommentaren und Stimmen; Rezensionen, die sich an verschiedene Benutzer richten; mindestens ein gegenseitiges Follow, damit die Listen der Follower und der Gefolgten eine Beziehung zeigen.

Der Seed-Prompt und der Seed-Authoring-Skill verlangen diese Abdeckung, damit eine Vorführung ohne manuelles Klicken lebendig wirkt. Es ist eine Schreibregel, keine Validierung: Ein Seed ohne Warenkorb besteht trotzdem.

## Woraus die Anwendung besteht

| Ordner | Denkweise der Eigentümerin |
|---|---|
| `Models/` | die Tabellen, eine Klasse pro Tabelle, mit bulgarischen Bezeichnungen |
| `Controllers/`, `Areas/…/Controllers/` | die Logik der Seiten |
| `Views/` | das Markup; `_Layout.cshtml` ist die gemeinsame Hülle |
| `Data/` | der Seed: `seed-data.json` ist lesbar und editierbar, `SeedData.cs` fügt ein |
| `Migrations/` | die Geschichte des Schemas |
| `wwwroot/` | CSS, JavaScript, heruntergeladene Bilder |
| `plan/state.json` | die Lesezeichen des Generators — Löschen erzwingt eine saubere Neuerzeugung |

## Was es nicht ist

Die Gestaltung ist Bootstrap plus gewähltes Theme und Design-Preset: stimmig und vorzeigbar, aber keine eigene Art-Direktion. Keine SPA, keine Echtzeit, keine Zahlungen, kein Mailversand. Jede dieser Grenzen ist bewusst; [Kapitel 22](22-limits.md) listet sie mit dem Unterstützten.

## Warum dieser Rundgang vor der Architektur steht

Drei Erwartungen entscheiden, wie der Rest gelesen wird: Das Ergebnis ist **vollständig** (Fehlendes ist ein Fehler oder eine dokumentierte Grenze); es ist **gewöhnlich** (MVC, EF Core, Razor, Identity); es ist **bulgarisch durch Konstruktion**, nicht durch Laufzeitübersetzung.

[Kapitel 9](09-workflow.md) zeigt, wie ein Lauf es erzeugt; [Kapitel 10](10-generated-app-architecture.md) zeigt die Schichtung.
