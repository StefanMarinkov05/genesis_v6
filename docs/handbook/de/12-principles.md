# Designprinzipien

Fünf Prinzipien erklären die meisten Entscheidungen in diesem Code. Wenn eine Änderung gegen die Architektur arbeitet, ist meist eines davon der Grund.

## 1. Determinismus ist die Vorgabe; Nichtdeterminismus wird zur Datei

Zwei Läufe derselben Lock-Datei ergeben dieselbe Anwendung, außer LLM-Inhalt und Migrationen. Deshalb ist Vielfalt ein **Hash**, nie `Random` oder `GetHashCode()` (in .NET prozessweise zufällig) — auch beim Design-Preset; jede Vorlage liest das aufgelöste Profil; das Modellergebnis ist eine validierte Datei.

## 2. Determinismus darf nicht wie Klonen aussehen

Jedes Projekt löst sein Profil aus seinem Seed:

| Achse | Werte |
|---|---|
| `collectionInit` | `new()` · `new List<T>()` · `[]` |
| `adminRoleName` | Administrator · Admin · Owner |
| `userRoleName` | das bulgarische Wort für „Nutzer“ · User · Client |
| `seedPasswordPattern` | drei Muster, alle konform zur Identity-Policy |
| `namespaceStyle` | file-scoped · block-scoped |
| `diRegistrationStyle` | verkettet · getrennte Anweisungen |
| `stringInterpolationVsConcat` | Interpolation · Konkatenation |
| `viewCommentDensity` | sparsam · moderat |
| `bootstrapTheme` | acht Bootswatch-Themes |
| `navbarStyle` | drei Konventionen |
| `buttonShape` | pill · rounded |

Elf Achsen, je ein Wert, gewählt als `SHA256(seed + ":" + axis)`. Die Spezifikation kann jede Achse pinnen; ungepinnte variieren zwischen Projekten und nie innerhalb eines Projekts. Die visuelle Identität jenseits von Bootstrap kommt aus einem Design-Preset-Overlay (ADR-008), sodass das „Aussehen“ eine Token-Wahl ist und kein Styling pro Vorlage. Wird `meta.style.preset` nicht gepinnt, wird das Preset mit demselben Hash unter dem Namen `designPreset` gewählt; es variiert also zwischen Projekten und nie zwischen Läufen derselben Spezifikation. (Früher wählte es `GetHashCode()`, das .NET pro Prozess randomisiert — behoben in Changelog 141, mit einem Test auf feste Sollwerte.)

Der Anlass ist konkret: Ein Entwickler liefert oft mehrere Projekte an dieselbe Schule, manchmal dieselbe Klasse, und die Arbeiten werden verglichen. Ohne Vielfalt bekäme jede Studierende dieselbe Anwendung — nutzlos als bewertetes Artefakt und unfair gegenüber denen, die sie abgegeben haben.

Die Grenze: Stil, Namen, Darstellung, Inhalt und Funktionsumfang variieren; die Seitenstruktur bleibt gleich, weil ein Vorlagensatz sie erzeugt.

## 3. Jede Stufe ist ein Checkpoint und idempotent

Ein Lauf kostet Minuten aus Bauen, Migrationen und LLM-Aufrufen und wird unterbrochen. Der Runner merkt sich Erledigtes je Stufe; jede Stufe erkennt ihre eigene Arbeit (vorhandenes `.csproj`, gültiger Seed, angewandte Migration).

## 4. Laut, früh und mit der Reparatur scheitern

Ein Anker, der seine Form nicht findet, wirft, statt das Gerüst unverändert zu lassen (ADR-006). `spec-expand` validiert vor dem Erzeugen; `verify` prüft das Manifest vor dem Start. `doctor` druckt den Reparaturbefehl, und der Agent stoppt bei Umgebungsproblemen.

## 5. Eine Implementierung, viele Frontends

Die CLI ist die Pipeline. Die GUI startet sie und rekonstruiert den Fortschritt aus `dps-nn.log.jsonl`; sie ruft keine Stufenklassen und implementiert kein Fortsetzen neu. Der Agentenmodus nutzt dieselbe Maschine ohne interaktive Auswahl.

## Die Schichtregel

Abhängigkeiten zeigen nach innen:

```
Genesis.Cli  ->  Agent, Stages, Diagnostics, Pipeline   (the composition root)
Agent        ->  Llm, Spec          (reaches the pipeline only through a delegate)
Stages       ->  Pipeline, Llm, Util, Variation, Spec
Diagnostics  ->  Pipeline
Pipeline     ->  Variation, Spec
Util         ->  Variation, Spec
Variation    ->  Spec
Spec, Llm    ->  (nothing inside the core)
```

- `Spec`, `Variation` und `Util` hängen von nichts in der Pipeline ab.
- `Llm` kennt den Dateivertrag, nicht die Stufen.
- `Stages` darf alle oben genannten nutzen; nichts außer der CLI nutzt `Stages`. Der Agent führt die Pipeline über einen Delegate aus, den die CLI ihm reicht, und genau das macht seine Schleife ohne `dotnet` testbar.
- `Diagnostics` ist ein Blatt: Es prüft die Welt, es erzeugt nichts.

Eine neue Abstraktion, die `Spec` oder `Util` dazu bringt, eine Stufe zu importieren, ist ein Zeichen, dass die Änderung stattdessen in die Stufe gehört.

## Erweiterungspunkte

| Ziel | Ergänzung | Kosten |
|---|---|---|
| Inhalt einer erzeugten Datei ändern | Vorlagenänderung | niedrig |
| neue Datei | Vorlage + ein Aufruf | niedrig |
| wiederkehrende Fähigkeit | `templates/features/<name>.json` + bedingte Ausgabe | mittel |
| neue Stufe | `IStage` + CLI-Verdrahtung | mittel |
| neue Spezifikationsfähigkeit | Schema + POCO + Semantik + Stufe + Doku | hoch |
