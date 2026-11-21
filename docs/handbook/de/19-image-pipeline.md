# Bildpipeline

Gekürzt aus dem vollständigen Kapitel der englischen Ausgabe (ADR-011).

## Das Problem

Bilder in einer Beispielanwendung sollen echt aussehen, Seiten nicht brechen, wenn das Netz fehlt, und nicht bei jeder Erzeugung erneut Kosten verursachen.

## Die Lösung

| Schritt | Was geschieht |
|---|---|
| 1. Phrasen | Die Seed-Zeile trägt `imageKeywords` — 1 bei `single`, 2–4 bei `multiple` |
| 2. Laden | eine Datei pro Phrase; Anbieter Pexels, Pixabay, Picsum oder lokaler Ordner |
| 3. Cache | `.image-cache/`, Schlüssel „Anbieter+Phrase“, außerhalb von `out/` |
| 4. Benennung | `{model}-{row}-{img}.jpg` — dieselbe Formel in Stufe und Vorlage |
| 5. Auslieferung | `wwwroot/images/…`; die Anwendung verlinkt nie extern |
| 6. Ersatz | mitgelieferte `avatar-default.svg` und `product-default.svg` |

## Warum kein Hot-Linking

Eine externe URL bricht bei Offline-Demo, langsamem Netz oder Anbieterwechsel. Eine lokale Datei ist Teil des Projekts und im Repository sichtbar.

## Wo beide Seiten zusammentreffen

`SeedStage` lädt und benennt; `seed_data.sbn` fügt je Array-Element eine `{Model}Image`-Zeile ein. Beide berechnen dasselbe Muster unabhängig — ändert sich die eine Seite, muss die andere geprüft werden.

## Grenzen

Keine Bildverarbeitung (Größe, Kompression, Zuschnitt), keine Inhaltsprüfung, Anbieter sind Dritte und brauchen Schlüssel außer Picsum.
