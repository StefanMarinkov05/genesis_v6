# Die gelieferte Anwendung

Der Rest des Buches beschreibt, wie erzeugt wird. Dieses Kapitel beschreibt, was ein Käufer erhält — und eine Eigenschaft der Lieferung, die sonst nirgends steht: Das Projekt kann markiert werden.

## Was übergeben wird

| Artefakt | Was es ist |
|---|---|
| Projektordner | eine vollständige MVC-Lösung, baubar mit `dotnet build` |
| `Migrations/` | das Schema, bereit für `dotnet ef database update` |
| `Data/seed-data.json` + `SeedData.cs` | die Demodaten und der Code, der sie beim Start einfügt |
| `wwwroot/images/` | heruntergeladene Bilder und Ersatzbilder |
| `wwwroot/output/project-summary.json` | der Bericht: Einstellungen, Vielfalt, Codepointer |
| `verify-language-report.txt` | das Urteil der Sprachprüfung |
| das Banner + `logs/<Project>/seed-credentials.txt` | die URL, ein Admin- und ein gewöhnliches Konto auf dem Bildschirm, und jedes geseedete Konto mit seinem Passwort in der Datei |
| `dps-nn.log.jsonl`, `plan/state.json` | wie der Lauf verlief |

Der Käufer erhält nicht die Pipeline, die Vorlagen oder die Spezifikationswerkzeuge — die bleiben beim Verkäufer. Das Gelieferte ist ein normales .NET-Projekt, das der Käufer besitzt und erweitern kann.

## Funktionsinventar

| Fähigkeit | Eingeschaltet durch | Geliefert wird |
|---|---|---|
| Konten | `identity` | Registrierung, Anmeldung, Abmeldung, Passwort- und E-Mail-Wechsel, Profil |
| Rollen und Bereiche | `identity.roles`, `areas[]` | Admin- und Client-Bereich, rollenbezogene Autorisierung |
| Eigentum pro Datensatz | `controllers[].ownership` | nur Eigentümer oder Admin, geprüft in der Action |
| Selbstlöschschutz | Benutzer-Controller | ein Administrator kann sich nicht löschen |
| Inhaltsmodelle | `models[]` | Entitäten, Beziehungen, Validierung, Audit, Soft Delete |
| Beiträge, Kommentare, Stimmen | `features.posts` | Beiträge erstellen, lesen und löschen, Kommentare mit optionalem Bild (nicht verschachtelt), Abstimmung |
| Kataloge | `features.catalogues` | persönliche Kataloge mit Artikeln, öffentliche Seiten |
| Warenkorb und Bestellungen | `features.cart` (nur Warenkorb) oder `features.orders` (mit Bestellung) | Warenkorb; mit `orders` zusätzlich Bestellung, Historie, Positionen |
| Rezensionen | `features.rating` | Rezensionen mit optionalem Bild |
| Follows | `features.follows` | Folgen und Entfolgen, Listen der Follower und der Gefolgten; sich selbst zu folgen wird abgelehnt |
| Öffentliche Profile | `features.posts` oder `features.catalogues` | eine öffentliche Profilseite pro Benutzer, die dessen Inhalte bündelt, auch auf der Detailseite des Benutzers im Admin-Bereich verwendet |
| Avatare | ein `ProfilePicturePath` an `identity.applicationUser` | Upload des Profilbilds und das Benutzer-Widget im Layout |
| Mehrere Bilder | `models[].images` | Bildtabelle, Upload, Galerie, Cache |
| Einzelbild | einfaches `ImagePath` | ein optionales Bild |
| Suche und Sortierung | `views.pages[]`, `controllers[].sorting` | serverseitige Suche über die Textfelder des Modells und serverseitige Sortierung, beide als Query-String-Parameter |
| Paging | `controllers[].pagination` | paginierte Admin-Tabellen |
| CRUD für jedes Modell | automatisch | Admin-Oberfläche je Modell |
| Sammelseite | `isSpecification`, `isLookup` | Tabs für kleine und Verknüpfungsmodelle |
| Responsive Darstellung | `meta.style` | Bootstrap, Theme, Preset, gemeinsames Layout |
| Befüllte Datenbank | `seeding`, Seed-JSON | realistische Zeilen, Eigentum, Bilder |
| Maschinenbericht | `outputReport` | Codepointer bis Datei und Zeile |

## Was es nicht ist

Eine Anwendung auf Demo-Niveau: kein Testprojekt, kein Dockerfile, keine CI, keine Zahlungen, kein Mailversand, keine Echtzeit, kein Sprachwechsel. Die Sicherheit ist die Vorgabe des Frameworks. [Kapitel 22](22-limits.md) enthält die vollständige Liste.

## Eigentumsmarken

Ein geliefertes Projekt kann **markiert** werden: Die Urheberschaft des Verkäufers wird in den Code eingebettet, sodass die Anwendung auf die Person zurückgeführt werden kann, die sie erstellt hat, ohne sich auf einen schriftlichen Vertrag, einen Dateinamen oder das Gedächtnis eines Kunden zu stützen.

Drei Tatsachen gehören zu jeder ehrlichen Beschreibung der Lieferung:

1. **Genesis markiert nichts.** Die Pipeline hat keinen Markierungscode, kein Spezifikationsfeld und keinen Kommandozeilenschalter dafür; jedes Projekt, das sie erzeugt, ist ein sauberer Build, der sich genau so verhält, wie dieses Buch es beschreibt.
2. **Marken werden nachträglich angebracht, bewusst und pro Projekt**, mit gesondertem Werkzeug, das der Verkäufer privat hält. Ein Käufer kann eine Marke nicht über die Spezifikation anfordern, und ein Dritter kann sie daraus nicht reproduzieren.
3. **Eine Marke ist ein Beleg, keine Funktion.** Sie existiert, damit die Urheberschaft gezeigt werden kann, wenn eine Lieferung bestritten oder nicht bezahlt wird. Das Mittel in diesem Fall ist die Vereinbarung zwischen Käufer und Verkäufer, nicht die Software.

Das Markierungswerkzeug wird hier nicht dokumentiert. Was einem Käufer vor der Zahlung gesagt werden sollte:

- ob das Projekt, das er erhält, eine Marke trägt;
- Prüfergebnisse, die der Verkäufer meldet, stammen aus sauberen Builds, sofern nicht anders angegeben.

## Belege, die mitreisen

Zwei Dinge machen ein geliefertes Projekt im Nachhinein prüfbar: Der Bericht (`project-summary.json`) hält Einstellungen, aufgelöste Vielfalt und den Ort jedes genannten Verhaltens im Code fest; das Laufprotokoll des Projekts hält die Stufen und ihre Zeiten fest. Der Hash jedes LLM-Austauschs steht im Log des Arbeitsordners (`dps-nn.log.jsonl` an dessen Wurzel), das beim Verkäufer bleibt, sofern es nicht übergeben wird. Zusammen beantworten sie „was hat das erzeugt, und wann“, ohne dass der Verkäufer anwesend sein muss.

Was sie **nicht** belegen: dass die Anwendung fachlich korrekt ist. `verify` zeigt, dass sie baut, migriert, startet, sich anmeldet dass jede deklarierte Seite antwortet, dass geschützte Seiten anonyme Benutzer abweisen und dass die Seiten kein Englisch zeigen ([Kapitel 9](09-workflow.md)). Alles danach ist Sache des Käufers und gehört vor die Abnahme.
