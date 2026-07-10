using System.Text.Json;
using Genesis.Core.Pipeline;
using Genesis.Core.Spec;
using Genesis.Core.Util;

namespace Genesis.Core.Stages;

/// <summary>
/// Stage 3 `scaffold`: dotnet new sln + mvc, sln add, NuGet package adds, first build.
/// Idempotency: presence of the .csproj short-circuits `dotnet new`; `dotnet add package`
/// is naturally idempotent (updates the reference).
/// </summary>
public sealed class ScaffoldStage : IStage
{
    public string Name => "scaffold";

    // All 10.0.* pinned - the generated app must restore identically months later.
    private static readonly string[] Packages =
    [
        "Microsoft.VisualStudio.Web.CodeGeneration.Design --version 10.0.*",
        "Microsoft.AspNetCore.Identity.UI --version 10.0.*",
        "Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.*",
        "Microsoft.EntityFrameworkCore.SqlServer --version 10.0.*",
        "Microsoft.EntityFrameworkCore.Tools --version 10.0.*",
        // Explicit direct references to force NuGet's resolution up from whatever vulnerable
        // 6.12.1 the codegen/EF tooling above transitively pulls in (GHSA-g4vj-cjjj-v7hg,
        // low severity, patched at 6.12.5 - NuGet always resolves to the HIGHEST version
        // requested anywhere in the graph, so this "upgrades" the transitive one too).
        "NuGet.Packaging --version 6.12.5",
        "NuGet.Protocol --version 6.12.5",
    ];

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        Directory.CreateDirectory(ctx.WorkDir);
        // .NET 10 `dotnet new sln` creates .slnx by default - accept whichever exists.
        var slnCandidates = new[] { $"{spec.Meta.SolutionName}.sln", $"{spec.Meta.SolutionName}.slnx" }
            .Select(f => Path.Combine(ctx.WorkDir, f)).ToArray();
        var csprojPath = Path.Combine(ctx.ProjectDir, $"{spec.Meta.ProjectName}.csproj");

        if (!slnCandidates.Any(File.Exists))
            (await Proc.RunAsync("dotnet", $"new sln -n {spec.Meta.SolutionName}", ctx.WorkDir, ct)).ThrowIfFailed("dotnet new sln");
        var slnPath = slnCandidates.First(File.Exists);

        if (!File.Exists(csprojPath))
            (await Proc.RunAsync("dotnet",
                $"new mvc -n {spec.Meta.ProjectName} --framework {spec.Meta.TargetFramework} --auth None",
                ctx.WorkDir, ct)).ThrowIfFailed("dotnet new mvc");

        (await Proc.RunAsync("dotnet", $"sln \"{slnPath}\" add \"{csprojPath}\"", ctx.WorkDir, ct))
            .ThrowIfFailed("dotnet sln add"); // idempotent: re-add is a no-op warning

        foreach (var pkg in Packages)
            (await Proc.RunAsync("dotnet", $"add \"{csprojPath}\" package {pkg}", ctx.WorkDir, ct))
                .ThrowIfFailed($"dotnet add package {pkg}");

        // Belt-and-braces alongside the explicit NuGet.Packaging/Protocol version bump above:
        // suppress NU1901 (low severity audit warning) directly too, in case the bump doesn't
        // fully silence it for some reason. Matches the same NoWarn convention this tool's own
        // csproj already uses for NU1902-1904 (see Genesis.Core.csproj/Genesis.Cli.csproj).
        var csprojDoc = System.Xml.Linq.XDocument.Load(csprojPath);
        var firstPropertyGroup = csprojDoc.Root!.Element("PropertyGroup");
        if (firstPropertyGroup?.Element("NoWarn") is { } existingNoWarn)
        {
            if (!existingNoWarn.Value.Contains("NU1901")) existingNoWarn.Value += ";NU1901";
        }
        else
        {
            firstPropertyGroup?.Add(new System.Xml.Linq.XElement("NoWarn", "$(NoWarn);NU1901"));
        }
        csprojDoc.Save(csprojPath);

        // Pin the SDK inside the GENERATED repo too - self-contained requirement.
        if (spec.Meta.SdkPin is { } pin)
            await File.WriteAllTextAsync(Path.Combine(ctx.WorkDir, "global.json"),
                JsonSerializer.Serialize(new { sdk = new { version = pin, rollForward = "latestFeature" } },
                    new JsonSerializerOptions { WriteIndented = true }), ct);

        // Connection string the generated Program.cs expects ({Project}ContextConnection).
        // LocalDB default - overridable via env: ConnectionStrings__{Project}ContextConnection.
        // Database name includes a timestamp suffix (not just the bare project name) so a
        // fresh generation never silently reuses/collides with an old database of the same
        // name left behind by an earlier tool version or an earlier run (Stefan hit exactly
        // this: a "FaceNote" LocalDB database already existed from a much older v3 run).
        // This only computes ONCE per genuinely new generation attempt - PipelineRunner's own
        // state.json skip-check means scaffold never re-runs on a --resume of the same attempt,
        // so the suffix stays stable for the lifetime of one generation.
        // (Deliberately NOT a live "does this name already exist" query against LocalDB/SQL
        // Server: that needs a new package dependency and a live connection assumption neither
        // of which can be compile- or run-verified here - unconditional uniqueness achieves the
        // same practical goal, "never collide with an old database", without that risk.)
        var dbName = $"{spec.Meta.ProjectName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var appsettingsPath = Path.Combine(ctx.ProjectDir, "appsettings.json");
        var appsettings = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(appsettingsPath, ct))!.AsObject();
        var cs = appsettings["ConnectionStrings"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();
        cs[$"{spec.Meta.ProjectName}ContextConnection"] =
            $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        appsettings["ConnectionStrings"] = cs;
        await File.WriteAllTextAsync(appsettingsPath, appsettings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), ct);
        ctx.Info($"database name: {dbName} (unique per generation - see appsettings.json to reuse an existing one manually)");

        // launchBrowser:true on every profile - `dotnet run` auto-opens a tab on startup,
        // complementing the console URL print in program.sbn (which works even where a
        // browser can't auto-launch, e.g. over a bare remote shell).
        var launchSettingsPath = Path.Combine(ctx.ProjectDir, "Properties", "launchSettings.json");
        if (File.Exists(launchSettingsPath))
        {
            var launchSettings = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(launchSettingsPath, ct))!.AsObject();
            if (launchSettings["profiles"]?.AsObject() is { } profiles)
                foreach (var (_, profile) in profiles)
                    if (profile?.AsObject() is { } p && p.ContainsKey("applicationUrl"))
                        p["launchBrowser"] = true;
            await File.WriteAllTextAsync(launchSettingsPath,
                launchSettings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), ct);
        }

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("initial dotnet build");
        ctx.Info("scaffold complete, connection string set, initial build green");
    }
}

/// <summary>
/// Stage 4 `emit models`: models, DTOs, {Project}Context, custom validation
/// attributes - all Scriban-rendered. Bulgarian error messages come straight from
/// the spec; templates never contain UI literals of their own.
/// </summary>
public sealed class EmitModelsStage : IStage
{
    public string Name => "emit-models";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        if (spec.Conventions.Timestamps.CreatedAt || spec.Conventions.Timestamps.UpdatedAt)
            await WriteAsync(ctx, "Models/AuditableEntity.cs",
                engine.RenderFile(ctx.Template("models/auditable_entity.sbn")), ct);

        foreach (var model in spec.Models)
            await WriteAsync(ctx, model.File, engine.RenderFile(ctx.Template("models/model.sbn"), new { model }), ct);

        foreach (var dto in spec.Dtos)
            await WriteAsync(ctx, dto.File, engine.RenderFile(ctx.Template("models/dto.sbn"), new { dto }), ct);

        if (spec.CustomValidationAttributes is { } cva)
            await WriteAsync(ctx, cva.File,
                engine.RenderFile(ctx.Template("models/validation_attributes.sbn"), new { cva }), ct);

        // ApplicationUser must exist before the DbContext that references it
        // (identity stage re-renders it later - idempotent overwrite, harmless).
        if (spec.Identity is { } idn)
            await WriteAsync(ctx, idn.ApplicationUser.File,
                engine.RenderFile(ctx.Template("identity/application_user.sbn")), ct);

        // TravelMap style: {ProjectName}Context at the project root, not Data/ApplicationDbContext.
        await WriteAsync(ctx, $"{spec.Meta.ProjectName}Context.cs",
            engine.RenderFile(ctx.Template("models/dbcontext.sbn")), ct);

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after emit-models");
        ctx.Info("models emitted, build green");
    }

    internal static async Task WriteAsync(StageContext ctx, string relPath, string content, CancellationToken ct)
    {
        var full = Path.Combine(ctx.ProjectDir, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, ct);
        ctx.Info($"wrote {relPath}");
    }
}

/// <summary>
/// Stage 5 `identity`:
///  1. aspnet-codegenerator identity with the spec's page modules
///  2. ApplicationUser (extends IdentityUser, spec extras) - template
///  3. BgIdentityErrorDescriber - template (runtime error strings)
///  4. Static EN->BG replacement map over scaffolded Identity Razor pages (ADR-002)
///     - the two Bulgarian surfaces are DIFFERENT mechanisms on purpose.
///  5. Roles + seed users via in-context seeding hook, areas + route registration
///     (Program.cs patching happens here since identity owns service registration).
/// </summary>
public sealed class IdentityStage : IStage
{
    public string Name => "identity";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        if (spec.Identity is not { } id) { ctx.Info("no identity section - skipping"); return; }
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // ApplicationUser + DbContext already reference each other; emit user first.
        await EmitModelsStage.WriteAsync(ctx, id.ApplicationUser.File,
            engine.RenderFile(ctx.Template("identity/application_user.sbn")), ct);

        if (id.ErrorDescriber is { } ed)
            await EmitModelsStage.WriteAsync(ctx, ed.File,
                engine.RenderFile(ctx.Template("identity/bg_identity_error_describer.sbn")), ct);

        // Program.cs references ImageService and SeedData - emit both now; later stages
        // re-emit them (idempotent overwrite). Program.cs is the convergence point: every
        // symbol it mentions must exist by this stage's build gate.
        if (spec.Models.Any(m => m.Images is not null))
            await EmitModelsStage.WriteAsync(ctx, "Services/ImageService.cs",
                engine.RenderFile(ctx.Template("controllers/image_service.sbn")), ct);
        // Same reason: Program.cs registers FileUploadService when pfp/rating/posts uploads exist, so the
        // class must exist by this build gate too (mirrors EmitControllersViewsStage's emit condition).
        if ((spec.Identity?.ApplicationUser.ExtraProperties.Any(p => p.Name == "ProfilePicturePath") ?? false)
            || EmitControllersViewsStage.FeatureEnabled(spec, "rating")
            || EmitControllersViewsStage.FeatureEnabled(spec, "posts"))
            await EmitModelsStage.WriteAsync(ctx, "Services/FileUploadService.cs",
                engine.RenderFile(ctx.Template("controllers/file_upload_service.sbn")), ct);
        // Task 26/7: Program.cs registers UserProfileService (+ references UserProfileViewModel) when
        // posts/catalogues exist, so both must exist by this build gate too (mirrors the emit condition
        // in EmitControllersViewsStage + program.sbn's needs_profile_service).
        if (EmitControllersViewsStage.FeatureEnabled(spec, "posts")
            || EmitControllersViewsStage.FeatureEnabled(spec, "catalogues"))
        {
            await EmitModelsStage.WriteAsync(ctx, "Models/UserProfileViewModel.cs",
                engine.RenderFile(ctx.Template("models/user_profile_view_model.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Services/UserProfileService.cs",
                engine.RenderFile(ctx.Template("controllers/user_profile_service.sbn")), ct);
        }
        if (spec.Seeding is not null && !File.Exists(Path.Combine(ctx.ProjectDir, spec.Seeding.OutputFile)))
            await EmitModelsStage.WriteAsync(ctx, spec.Seeding.OutputFile,
                engine.RenderFile(ctx.Template("models/seed_data.sbn")), ct);

        // Program.cs is REPLACED wholesale from template (not patched): the MVC
        // template's Program.cs is tiny and ours must own DI order, identity,
        // areas routing, seeding hook, and MigrateAsync-on-startup flag.
        await EmitModelsStage.WriteAsync(ctx, "Program.cs",
            engine.RenderFile(ctx.Template("project/program.sbn")), ct);
        await EmitModelsStage.WriteAsync(ctx, "Data/IdentitySeeder.cs",
            engine.RenderFile(ctx.Template("identity/identity_seeder.sbn")), ct);

        // Scaffold the Identity UI pages the spec asks for.
        var modules = string.Join(";", id.Modules); // e.g. Account.Register;Account.Login;Account.Logout
        var gen = await Proc.RunAsync("dotnet",
            $"aspnet-codegenerator identity -dc {spec.Meta.RootNamespace}.{spec.Meta.ProjectName}Context --files \"{modules}\"",
            ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5));
        if (gen.ExitCode != 0)
        {
            // codegenerator needs the tool installed; give the actionable command.
            throw new InvalidOperationException(
                "aspnet-codegenerator failed. Install once: dotnet tool install -g dotnet-aspnet-codegenerator\n" + gen.StdErr);
        }

        // Bulgarian page markup, two layers (ADR-002b):
        //  a) replacement map over ALL scaffolded pages (fallback for untemplated pages)
        if (id.Localization is { Strategy: "staticReplacementMap" } loc)
            await ApplyReplacementMapAsync(ctx, Path.Combine(ctx.GenesisRoot, "templates/identity", loc.MapFile), ct);
        //  b) wholesale template emission of the three core page MARKUPS - the scaffolded
        //     .cshtml.cs code-behind (the logic) is kept untouched. Matches the reference style.
        foreach (var (page, tpl) in new (string, string)[]
        {
            ("Login", "identity/login_page.sbn"),
            ("Register", "identity/register_page.sbn"),
            ("Logout", "identity/logout_page.sbn"),
        })
        {
            var target = Path.Combine("Areas", "Identity", "Pages", "Account", page + ".cshtml");
            if (File.Exists(Path.Combine(ctx.ProjectDir, target)))
                await EmitModelsStage.WriteAsync(ctx, target,
                    engine.RenderFile(ctx.Template(tpl)), ct);
        }

        // Areas: physical folders + a placeholder _ViewStart per area.
        foreach (var area in spec.Areas)
        {
            var areaViews = Path.Combine(ctx.ProjectDir, "Areas", area.Name, "Views");
            Directory.CreateDirectory(Path.Combine(ctx.ProjectDir, "Areas", area.Name, "Controllers"));
            Directory.CreateDirectory(areaViews);
            await File.WriteAllTextAsync(Path.Combine(areaViews, "_ViewStart.cshtml"),
                "@{\n    Layout = \"/Views/Shared/_Layout.cshtml\";\n}\n", ct);
            await File.WriteAllTextAsync(Path.Combine(areaViews, "_ViewImports.cshtml"),
                $"@using {spec.Meta.RootNamespace}\n@using {spec.Meta.RootNamespace}.Models\n@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers\n", ct);
        }

        // Bulgarian DataAnnotations messages on the scaffolded code-behind InputModels
        // (Login/Register/etc .cshtml.cs) - ADR-002b leaves this code untouched otherwise,
        // so [Required]/[EmailAddress]/[Compare] fall back to English defaults without this.
        InjectBgValidationMessages(ctx);

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after identity");
        ctx.Info("identity complete, build green");
    }

    /// <summary>
    /// Adds ErrorMessage="..." (Bulgarian) to bare DataAnnotations attributes inside every
    /// scaffolded Identity .cshtml.cs InputModel. Idempotent: skips attributes that already
    /// carry an ErrorMessage. Regex-based like the deviation transforms (ADR-006) - the
    /// scaffolder's InputModel shape is stable per SDK pin.
    /// </summary>
    internal static void InjectBgValidationMessages(StageContext ctx)
    {
        var pagesDir = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages", "Account");
        if (!Directory.Exists(pagesDir)) return;

        // attribute name -> Bulgarian message template. {0} is filled from the property name
        // for the couple of messages that read better parameterized.
        var messages = new Dictionary<string, string>
        {
            ["Required"] = "Полето {0} е задължително.",
            ["EmailAddress"] = "Невалиден имейл адрес.",
            ["Phone"] = "Невалиден телефонен номер.",
            ["Compare"] = "Паролите не съвпадат.",
        };
        // Bulgarian display names for the property names we know appear in scaffolded InputModels.
        var displayNames = new Dictionary<string, string>
        {
            ["Email"] = "Имейл", ["Password"] = "Парола", ["ConfirmPassword"] = "Потвърдете паролата",
            ["OldPassword"] = "Старата парола", ["NewPassword"] = "Новата парола", ["PhoneNumber"] = "Телефон",
            ["Code"] = "Код", ["TwoFactorCode"] = "Код за двуфакторна автентикация", ["RecoveryCode"] = "Код за възстановяване",
        };

        foreach (var file in Directory.EnumerateFiles(pagesDir, "*.cshtml.cs", SearchOption.TopDirectoryOnly))
        {
            var source = File.ReadAllText(file);
            var changed = false;

            // Attributes without any parentheses at all: [Required] -> [Required(ErrorMessage = "...")]
            // We look one line ahead for "public ... PropName { get; set; }" to know which property
            // the attribute decorates (for the display-name substitution) and whether it already
            // carries ErrorMessage (in which case we leave it alone - idempotent rerun).
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var (attr, template) in messages)
                {
                    var bareMatch = System.Text.RegularExpressions.Regex.Match(lines[i], $@"^(\s*)\[{attr}\]\s*$");
                    var withArgsNoMsg = System.Text.RegularExpressions.Regex.Match(lines[i],
                        $@"^(\s*)\[{attr}\(([^)]*)\)\]\s*$");

                    string? propName = null;
                    for (var j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
                    {
                        var propMatch = System.Text.RegularExpressions.Regex.Match(lines[j], @"public\s+\S+\s+(\w+)\s*\{");
                        if (propMatch.Success) { propName = propMatch.Groups[1].Value; break; }
                    }
                    var display = propName is not null && displayNames.TryGetValue(propName, out var dn) ? dn : propName ?? "";
                    var message = string.Format(template, display);

                    if (bareMatch.Success)
                    {
                        lines[i] = $"{bareMatch.Groups[1].Value}[{attr}(ErrorMessage = \"{message}\")]";
                        changed = true;
                    }
                    else if (withArgsNoMsg.Success && !withArgsNoMsg.Groups[2].Value.Contains("ErrorMessage"))
                    {
                        lines[i] = $"{withArgsNoMsg.Groups[1].Value}[{attr}({withArgsNoMsg.Groups[2].Value}, ErrorMessage = \"{message}\")]";
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                File.WriteAllText(file, string.Join('\n', lines));
                ctx.Info($"injected BG validation messages into {Path.GetFileName(file)}");
            }
        }
    }

    /// <summary>
    /// identity-bg-map.json format: { "sdk": "10.0.100", "replacements": { "Log in": "Вход", ... } }
    /// Longest-key-first application so "Log in with" never gets clobbered by "Log in".
    /// </summary>
    internal static async Task ApplyReplacementMapAsync(StageContext ctx, string mapPath, CancellationToken ct)
    {
        if (!File.Exists(mapPath))
        {
            ctx.Info($"WARNING: identity map {mapPath} missing - Identity pages remain English. Verify WILL fail on them.");
            return;
        }
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(mapPath, ct));
        var pairs = doc.RootElement.GetProperty("replacements").EnumerateObject()
            .Select(p => (From: p.Name, To: p.Value.GetString()!))
            .OrderByDescending(p => p.From.Length)
            .ToList();

        var identityPagesDir = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages");
        if (!Directory.Exists(identityPagesDir)) return;
        foreach (var file in Directory.EnumerateFiles(identityPagesDir, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = await File.ReadAllTextAsync(file, ct);
            foreach (var (from, to) in pairs) text = text.Replace(from, to);
            await File.WriteAllTextAsync(file, text, ct);
        }
        ctx.Info($"identity BG map applied ({pairs.Count} replacements)");
    }
}
