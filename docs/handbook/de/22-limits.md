# Fähigkeiten und Grenzen

Ein Generator wird ebenso durch das definiert, was er verweigert. Dieses Kapitel ist das ehrliche Register.

## Was heute möglich ist

| Fähigkeit | Eingeschaltet durch | Zustand |
|---|---|---|
| Entitäten mit typisierten Eigenschaften, Schlüsseln, nullable, Enums | `models[]` | erprobt |
| Eins-zu-viele-Beziehungen, Löschverhalten | `relations[]` | erprobt |
| Bulgarische Bezeichnungen und Validierungsmeldungen | `displayName`, `validations[]` | ein fehlendes Label weist die semantische Validierung ab; durchgerutschtes Englisch fängt das Sprachgate |
| Datumsregeln | `customValidationAttributes` | echte C#-Attribute |
| Zeitstempel, Soft Delete | `conventions` | erprobt |
| Rollen, Bereiche, rollenbezogene Autorisierung | `identity.roles`, `areas[]` | erprobt |
| Seed-Nutzer mit abgeleiteten Passwörtern | `identity.seedUsers` | erprobt |
| Erweitertes Nutzerprofil samt Controller | `identity.applicationUser` | erprobt (Profilfelder und Avatar von FaceNote) |
| Identitätsseiten bulgarisch | `identity.modules` | erprobt |
| CRUD für jedes Modell in beiden Bereichen | `controllers[]` + Injektion | erprobt |
| Deklarative Abweichungen (Aktionen, Weiterleitungen, Sortierung, Paging, Eigentum) | `controllers[]` | erprobt |
| Start-, Kontakt-, Kartenraster-, Admin-Tabellenseiten | `views.pages[]` | erprobt |
| Galerien mit Upload und Cache | `models[].images` | erprobt (`multiple`) |
| Wiederverwendbare Funktionen: Kataloge, Bestellungen (mit ihrem Warenkorb), Beiträge, Follows, Rating, Personalisierung | `features.<name>` | gemeinsam in der Fixture erprobt |
| Ein Warenkorb ohne Bestellabschluss | `features.cart` (ohne `orders`) | Expansion und Vorlagen per Unit-Test geprüft; noch kein vollständiger Windows-Lauf |
| Realistische Seed-Daten mit symbolischen Verweisen | `seeding`, Seed-JSON | erprobt |
| Maschinenlesbarer Bericht mit Codepointern | `outputReport` | erprobt bei Konfiguration |
| Englische Oberfläche | `meta.language = "en"` | teilweise (siehe unten) |

## Was es nicht kann

Strukturelle Lücken, nicht fehlende Politur. Wer sie verlangt, erhält einen `unsupportedRequests`-Eintrag statt eines stillen Weglassens.

- **Wiederkehrende Termine**: Buchungen, Kalender, Zeitpläne, Verfügbarkeiten.
- **Zahlungen und externe Integrationen**: Zahlungsanbieter, Versand-APIs, E-Mail-Versand, SMS, Webhooks, externe Identitätsanbieter.
- **Echtzeit**: Chat, Benachrichtigungen, Hintergrundjobs, Queues.
- **Andere Persistenz als SQL Server.** `meta.database.provider` akzeptiert nur `SqlServer`.
- **Viele-zu-viele ohne Verknüpfungsentität**, polymorphe Beziehungen, zusammengesetzte Schlüssel, GUID-Schlüssel für Domänenmodelle.
- **Import/Export, Berichte, PDF, Dateiformate** jenseits von Bild-Upload.
- **Zweite Sprache zur Laufzeit.** Es gibt keinen Locale-Schalter; bulgarischer Text wird ausgegeben.

## Fallstricke, die überraschen

| Fallstrick | Detail |
|---|---|
| `personalization` ist ein Layout-Schalter, keine Datenfunktion | sie fügt keine Modelle hinzu; sie schaltet in der Navigationsleiste Begrüßung und Avatar ein (und einen Link „Mein Profil“, wenn `posts` oder `catalogues` Profilseiten liefern). Profile selbst kommen aus `posts`/`catalogues`, Avatare aus einem `ProfilePicturePath` am Benutzermodell; für eine schlichte Leiste ausschalten |
| `verify` prüft Zugriff, nicht Eigentum | anonyme Benutzer müssen von Admin- und anmeldepflichtigen Seiten abgewiesen werden, aber nichts prüft, dass ein angemeldeter Benutzer nicht an die Datensätze eines anderen kommt. Für benutzereigene Modelle Eigentumsabweichungen deklarieren und von Hand testen |
| `language: "en"` ist teilweise | bulgarische Durchgänge entfallen, aber viele Vorlagen enthalten bulgarische Literale — die Oberfläche wird gemischt |
| DTOs sind verdrahtet, aber unerprobt | Schema, Ausgabe und Manifest funktionieren, keine Fixture nutzt eins |
| `images.mode: "single"` sieht wie multiple aus | der Dienst löscht korrekt, die UI zeigt in beiden Modi eine Galerie |
| Der Umfang ist real | 30 Modelle werden erzeugt, aber Demo und Prüfaufwand wachsen schneller als der Nutzen |
| Windows ist die unterstützte Umgebung | LocalDB und Installer sind Windows; der Generator testet auch unter Linux, die Erzeugung endet dort |
| Sicherheit auf Demo-Niveau | kein Rate Limiting, keine CSP, Development-Verbindungen |
| Bildanbieter sind Dritte | Pexels und Pixabay brauchen Schlüssel; Picsum ist frei, aber beliebig |
| Das Sprachgate ist eine Heuristik | 2+ lateinische Wörter mit einem häufigen englischen; Marken passieren, ein Satz nicht |

## Eignungstest

1. Ist die Domäne CRUD-förmig — Entitäten, Beziehungen, Listen, Admin-Tabellen, wenige geschützte Abläufe?
2. Ist eine bulgarische Oberfläche akzeptabel (oder genau gewünscht)?
3. Liegt der Wert der Demo in Breite und Vollständigkeit, nicht in einem Sonderalgorithmus?
4. Kann das Projekt für die Erzeugung unter Windows laufen?
5. Ist die Eigentümerin bereit, den erzeugten Code zu erklären?

## Wie das Register wächst

`meta.unsupportedRequests`: Jede nicht ausdrückbare Anforderung wird festgehalten, wiederkehrende werden neue Feature-Fragmente. Deshalb ist die Liste kurz und konkret — ein Plan, kein Urteil.

## Was das Design schon möglich macht

- **Der Bericht ist eine Nachverfolgungskarte.** `report` löst Codepointer zu Datei und Zeile auf — Rohmaterial für ein erzeugtes Projektdokument: ein Kapitel pro Anforderung, das auf den Code zeigt.
- **`unsupportedRequests` ist ein offener Kreis, der darauf wartet, geschlossen zu werden.** Heute hält die Liste fest, was der Generator nicht ausdrücken konnte. Kehrt dieselbe Anforderung über mehrere Läufe wieder, ist der Eintrag die Spezifikation für das nächste Funktionsfragment. Der Abstand zwischen „das Werkzeug kann X nicht“ und „das Werkzeug hat X gelernt“ ist eine Review-Warteschlange, kein Neuschreiben.
- **Der Prüfer ist das Produkt.** Dasselbe Gate kann ein anderes Backend oder einen anderen Stack überwachen, weil es Ergebnisse prüft, nicht Interna. Es scheitert inzwischen an jeder deklarierten Route, die nicht antwortet, und an jeder geschützten Seite, die eine anonyme Anfrage öffnen kann; eine Eigentumsprüfung pro Benutzer ist der nächste Schritt.

Die drei Schritte mit dem größten Effekt: ein erzeugtes Projektdokument aus dem Bericht; eine vom Werkzeug geprüfte Fähigkeitsmatrix; ein zweites Backend hinter derselben Spezifikation.
