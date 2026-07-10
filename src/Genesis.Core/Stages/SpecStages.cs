using System.Text.Json;
using System.Text.Json.Nodes;
using Genesis.Core.Llm;
using Genesis.Core.Pipeline;
using Genesis.Core.Spec;
using Genesis.Core.Variation;
using Json.Schema;

namespace Genesis.Core.Stages;

/// <summary>
/// Stage 1 `spec build`: raw notes file -> draft project-spec.json via LLM.
/// SKIPPED entirely when the user supplies a ready spec (the DPS-JSON flow):
/// `genesis run --all --spec path.json` starts at spec-expand.
/// Always prompts interactively for conventions.timestamps unless the spec
/// pins them and --no-interactive is set.
/// </summary>
public sealed class SpecBuildStage(LlmBridge llm, string notesPath, string specOutPath, string schemaPath,
    Func<string, string> promptUser) : IStage
{
    public string Name => "spec-build";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        if (File.Exists(specOutPath)) { ctx.Info($"spec exists at {specOutPath}, skipping build"); return; }

        var notes = await File.ReadAllTextAsync(notesPath, ct);
        var schema = JsonSchema.FromFile(schemaPath);

        var doc = await llm.ExchangeAsync("spec-build",
            "Draft a genesis project-spec from these raw project notes. Bulgarian UI language. " +
            "Infer models, validations (with Bulgarian errorMessages), controllers, views, features.",
            schema, new { notes }, ct);

        var node = JsonNode.Parse(doc.RootElement.GetRawText())!;

        // conventions.timestamps is a per-project judgment call - always ask.
        foreach (var key in new[] { "createdAt", "updatedAt", "deletedAt" })
        {
            var answer = promptUser($"conventions.timestamps.{key}? [y/n]: ");
            node["conventions"]!["timestamps"]![key] = answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        await File.WriteAllTextAsync(specOutPath, node.ToJsonString(ProjectSpec.JsonOpts), ct);
        ctx.Info($"spec written: {specOutPath}");
    }
}

/// <summary>
/// Stage 2 `spec expand` (ADR-001): deterministic feature expansion, then one
/// optional LLM review pass whose suggestions require explicit y/n each.
/// Input:  project-spec.json (immutable)
/// Output: project-spec.lock.json - the ONLY spec later stages read, and the
///         expected-file manifest verify checks against.
/// Also resolves: $variation:* placeholders, seed-user passwords.
/// </summary>
public sealed class SpecExpandStage(SpecLoader loader, string specPath, string lockPath, string featureTemplatesDir,
    LlmBridge? llmReview, Func<string, string> promptUser) : IStage
{
    public string Name => "spec-expand";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = loader.Load(specPath);
        var variation = VariationResolver.Resolve(spec);
        var node = JsonNode.Parse(await File.ReadAllTextAsync(specPath, ct))!.AsObject();

        // 1. Deterministic feature expansion: each enabled feature merges its
        //    models/controllers/views fragments from feature-templates/{name}.json.
        foreach (var (feature, cfg) in spec.Features)
        {
            if (!cfg.TryGetProperty("enabled", out var en) || !en.GetBoolean()) continue;
            var fragmentPath = Path.Combine(featureTemplatesDir, $"{feature}.json");
            if (!File.Exists(fragmentPath))
            {
                ctx.Info($"feature '{feature}' enabled but no fragment at {fragmentPath} - skipping (built-in views only)");
                continue;
            }
            // Feature fragments may reference the project's primary model via "$primaryModel"
            // (e.g. catalogues joins to Product in FaceNote, Destination in a travel project).
            // Explicit cfg.model wins; otherwise fall back to the first model with images -
            // this MUST match EmitControllersViewsStage.FeaturePrimaryModel's fallback, or a
            // feature enabled without an explicit "model" (legal per schema) leaves the literal
            // token "$primaryModel" in a relation target, which fails semantic validation.
            var fragmentText = await File.ReadAllTextAsync(fragmentPath, ct);
            var primaryModel = ResolvePrimaryModel(spec, cfg);
            fragmentText = fragmentText.Replace("$primaryModel", primaryModel);
            var fragment = JsonNode.Parse(fragmentText)!.AsObject();
            MergeFragment(node, fragment, feature, cfg, ctx);
        }

        // 2. Resolve $variation placeholders + seed passwords into the lock.
        var resolvedJson = variation.Substitute(node.ToJsonString(ProjectSpec.JsonOpts));
        var lockNode = JsonNode.Parse(resolvedJson)!.AsObject();
        if (lockNode["identity"]?["seedUsers"]?["users"] is JsonArray users)
            foreach (var u in users)
                u!["password"] ??= variation.SeedPassword(u["email"]!.GetValue<string>());
        lockNode["variationProfileResolved"] = JsonNode.Parse(JsonSerializer.Serialize(variation.Axes));

        // 3. Optional LLM review pass - suggestions gated behind y/n, never silent.
        if (llmReview is not null)
        {
            try
            {
                var suggestionSchema = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("suggestions", new JsonSchemaBuilder().Type(SchemaValueType.Array)
                        .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object)
                            .Properties(
                                ("path", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                ("patch", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)))
                            .Required("path", "description"))))
                    .Required("suggestions").AdditionalProperties(false).Build();

                var review = await llmReview.ExchangeAsync("expand-review",
                    "Review this expanded spec for a generated MVC app. Suggest MISSED attributes or classes only " +
                    "(domain-typical fields, obvious relations). Each suggestion: JSON pointer path + description + optional patch object.",
                    suggestionSchema, lockNode, ct);

                foreach (var s in review.RootElement.GetProperty("suggestions").EnumerateArray())
                {
                    var desc = s.GetProperty("description").GetString();
                    var answer = promptUser($"LLM suggestion: {desc} - apply? [y/n]: ");
                    if (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                        ctx.Info($"NOTE: apply suggestion manually or via patch: {s.GetRawText()}");
                        // Deliberately conservative: v1 logs accepted suggestions for manual
                        // merge instead of auto-patching arbitrary JSON pointers into the lock.
                }
            }
            catch (LlmBridge.LlmPendingException) { throw; }
        }

        await File.WriteAllTextAsync(lockPath, lockNode.ToJsonString(ProjectSpec.JsonOpts), ct);
        // Re-validate the LOCK too - expansion bugs must fail here, not at emit time.
        loader.Load(lockPath);
        ctx.Info($"lock written + validated: {lockPath}");
    }

    /// <summary>
    /// The project's primary model for a feature: explicit cfg.model wins, else the first model
    /// with images (same rule as EmitControllersViewsStage.FeaturePrimaryModel - MUST stay in sync,
    /// or a feature enabled without an explicit "model" resolves differently at expand-time vs
    /// emit-time and a fragment's "$primaryModel" token survives into semantic validation).
    /// </summary>
    internal static string ResolvePrimaryModel(ProjectSpec spec, JsonElement cfg) =>
        cfg.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()!
            : spec.Models.First(x => x.Images is not null).Name;

    /// <summary>Array-append merge for models/controllers; views.pages appended; name-collisions fail loud.</summary>
    internal static void MergeFragment(JsonObject spec, JsonObject fragment, string feature, JsonElement cfg, StageContext? ctx)
    {
        foreach (var section in new[] { "models", "dtos", "controllers" })
        {
            if (fragment[section] is not JsonArray add) continue;
            var target = spec[section]?.AsArray() ?? (JsonArray)(spec[section] = new JsonArray())!.AsArray();
            var existing = target.Select(t => t?["name"]?.GetValue<string>()).ToHashSet();
            foreach (var item in add.ToList())
            {
                var name = item?["name"]?.GetValue<string>();
                if (existing.Contains(name))
                    throw new InvalidOperationException($"Feature '{feature}' injects duplicate {section} entry '{name}'.");
                item!.Parent!.AsArray().Remove(item);
                target.Add(item);
            }
        }
        if (fragment["views"]?["pages"] is JsonArray pages)
        {
            var target = spec["views"]!["pages"]!.AsArray();
            foreach (var p in pages.ToList()) { p!.Parent!.AsArray().Remove(p); target.Add(p); }
        }
        ctx?.Info($"feature '{feature}' expanded");
    }
}
