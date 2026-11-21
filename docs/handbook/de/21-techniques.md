# Techniken und Fallen

Gekürzt aus dem vollständigen Kapitel der englischen Ausgabe; die ausführlichen Erklärungen stehen dort.

| Falle | Was geschieht | Was wir tun |
|---|---|---|
| Eine Scriban-Oberfläche | alle Vorlagen nutzen denselben Helfersatz | `TemplateEngine` ist die einzige Oberfläche |
| `@` in Razor-`<style>` | `@media` wird als C#-Ausdruck gelesen | als `@@media` maskieren |
| Altes `dotnet-ef` | Projekt baut, dann „deps.json does not exist“ | `dotnet tool update -g dotnet-ef --version 10.0.*` |
| MSBuild-Node-Wiederverwendung | schnelle aufeinanderfolgende Builds treffen denselben Node | `DOTNET_CLI_DISABLE_BUILD_SERVERS=1` für diese Aufrufe |
| Blinde EN→BG-Karte | übersetzt auch C#-Bezeichner | ganzen `Manage/`-Teilbaum ausnehmen |
| Reihenfolge der Migrationen | `ef migrations add` vor dem Build | immer Build → Migration → Update |
| Design-Preset nie per `GetHashCode()` (`ResolveDesignPreset`) | ohne gepinntes `meta.style.preset` wählte früher `GetHashCode()` das Preset, das .NET pro Prozess randomisiert; zwei Läufe derselben Spezifikation bekamen verschiedene Presets | behoben in Changelog 141: dasselbe `SHA256(seed:designPreset)` wie die Achsen, mit Sollwert-Test; bei neuer Auswahllogik nie wieder `GetHashCode()` |
| Lange LR-Diagramme im PDF | unlesbar im Hochformat | lange Ketten werden von oben nach unten gezeichnet |

## Das Prinzip

Jede Falle hier hat eine echte Stunde gekostet. Sie stehen hier, damit sie nicht zweimal bezahlt werden; der Volltext erklärt auch, warum die Alternative schlechter ist.
