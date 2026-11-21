# CLI-Referenz

Gekürzt aus der vollständigen Referenz der englischen Ausgabe; Kommandos und Codes sind identisch.

| Kommando | Aufgabe |
|---|---|
| `run --all` | die ganze Pipeline für eine Spezifikation; überspringt erledigte Stufen |
| `run --all --from <Stufe>` | startet diese Stufe und alle folgenden neu |
| `create` | Agentenmodus: Beschreibung → Entwurf → Lauf → Reparatur |
| `doctor` | prüft die Voraussetzungen und druckt die Reparatur |
| `spec build --notes <Datei>` | Entwurf aus Notizen |
| `spec expand` | Expansion + `$variation:` → `project-spec.lock.json` |
| `scaffold`, `emit-models`, `identity`, `emit-controllers-views`, `seed`, `verify`, `report` | eine einzelne Stufe (Diagnose) |

## Optionen

| Option | Standard | Bedeutung |
|---|---|---|
| `--spec <Pfad>` | `project-spec.json` | die Spezifikation |
| `--out <Ordner>` | `out` (`create`: `out/agent_<Zeit>`) | Ergebniswurzel |
| `--no-interactive` | aus | keine Fragen; fehlt eine Antwort, Fehler |
| `--resume` | aus | nach manuell gefüllter Antwort fortsetzen |
| `--image-provider`, `--image-pack` | `pexels` / – | Bildquelle |
| `--version` | – | Version und Commit aus `VERSION` |

## Exitcodes

| Code | Bedeutung |
|---|---|
| `0` | Erfolg |
| `1` | eine Stufe scheiterte; der Agent stoppte; `doctor` fand etwas |
| `2` | Fehlbedienung (keine Beschreibung, kein Schlüssel, falsche Option) |
| `3` | LLM-Austausch wartet auf eine manuelle Antwort |

## Umgebungsvariablen

| Variable | Wirkung |
|---|---|
| `ANTHROPIC_API_KEY` | automatische LLM-Austausche (sonst Dateivertrag, Code 3) |
| `DPS_NN_MODEL_SPEC` / `DPS_NN_MODEL_SEED` | Modellwechsel |
| `ANTHROPIC_BASE_URL` | anderer Endpunkt (Proxy, Test-Stub) |
| `PEXELS_API_KEY` / `PIXABAY_API_KEY` | echte Fotos (sonst Picsum) |
| `DPS_NN_LOG_LANGUAGE=en` | englische Thread-Namen im Log |
| `ConnectionStrings__DefaultConnection` | Datenbank der erzeugten Anwendung |

## Auswahl bei mehreren Spezifikationen oder Seeds

Ohne `--spec` erscheint bei mehreren `project-spec*.json` ein nummeriertes Menü; genau ein Kandidat wird automatisch genutzt; ein explizites `--spec` fragt nie. Seed-Kandidaten liegen in `seeds/` oder `prompts/out/seeds/`; die Auswahl wird nach `prompts/in/seed-response.json` kopiert. Unter `--no-interactive` entfallen beide Menüs.
