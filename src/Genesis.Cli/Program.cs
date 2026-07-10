using System.CommandLine;
using Genesis.Core.Cli;
using Genesis.Core.Llm;
using Genesis.Core.Pipeline;
using Genesis.Core.Spec;
using Genesis.Core.Stages;
using Genesis.Core.Variation;

// dps-nn (ДПС-НН) - declarative ASP.NET MVC project generator. (internal assembly name: Genesis.Cli)
// Command surface:
//   dps-nn spec build --notes notes.md --spec project-spec.json
//   dps-nn spec expand --spec project-spec.json
//   dps-nn scaffold|emit-models|identity|emit-controllers-views|seed|verify|report --spec ...
//   dps-nn run --all [--from <stage>] [--spec ...] [--out <dir>] [--image-pack <dir>] [--image-provider pexels|pixabay|picsum]
// LLM mode: automatic when ANTHROPIC_API_KEY is set, else manual file contract (--resume to continue).
// Interactive pickers (skipped entirely when --spec is explicit, or under --no-interactive):
//   - multiple project-spec*.json in cwd/fixtures/ -> numbered menu instead of the bare default
//   - multiple *.json under seeds/ -> numbered menu; the chosen file becomes prompts/in/seed-response.json
//     for that run (and any stale Data/seed-data.json in the target project is cleared so the choice sticks)
// Errors are logged, not just printed: a stage failure goes to logs/<Project>/actions.log.jsonl
// (ctx.Error, full exception detail); anything failing before a project context exists (spec
// resolution, GenesisRoot()) goes to logs/global-errors.jsonl instead.

var specOpt = new Option<FileInfo>("--spec", () => new FileInfo("project-spec.json"), "Path to project-spec.json");
var outOpt = new Option<DirectoryInfo>("--out", () => new DirectoryInfo("out"), "Output directory for generated projects");
var notesOpt = new Option<FileInfo>("--notes", "Raw notes file for spec build");
var fromOpt = new Option<string?>("--from", "Resume `run --all` from this stage");
var imagePackOpt = new Option<DirectoryInfo?>("--image-pack", "Local image directory override");
var imageProviderOpt = new Option<string>("--image-provider", () => "pexels", "pexels | pixabay | picsum");
var noInteractiveOpt = new Option<bool>("--no-interactive", "Never prompt; fail if a prompt would be required");
var resumeOpt = new Option<bool>("--resume", "Continue after manually filling prompts/in/<stage>-response.json");

var root = new RootCommand("dps-nn (ДПС-НН) - ASP.NET Core MVC project generator");
root.AddGlobalOption(specOpt);
root.AddGlobalOption(outOpt);
root.AddGlobalOption(noInteractiveOpt);
root.AddGlobalOption(resumeOpt);

string GenesisRoot()
{
    // Templates resolve relative to the tool's own install dir first, then cwd -
    // so `genesis` works both as a global tool and from the repo.
    var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
    foreach (var c in candidates)
        if (Directory.Exists(Path.Combine(c, "templates"))) return c;
    throw new InvalidOperationException("templates/ directory not found next to the tool or in cwd.");
}

StageContext BuildContext(FileInfo spec, DirectoryInfo outDir, bool useLock)
{
    var genesisRoot = GenesisRoot();
    var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
    var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
    var effective = useLock && File.Exists(lockPath) ? lockPath : spec.FullName;
    var loaded = loader.Load(effective);
    var variation = VariationResolver.Resolve(loaded);
    var state = new StateStore(Path.Combine(outDir.FullName, loaded.Meta.ProjectName, "plan", "state.json"));
    return new StageContext(loaded, variation, outDir.FullName, genesisRoot, state, Console.Out);
}

LlmBridge Llm(DirectoryInfo outDir, string? projectName = null) => new(
    promptsDir: Path.Combine(Directory.GetCurrentDirectory(), "prompts"),
    logPath: Path.Combine(Directory.GetCurrentDirectory(), "dps-nn.log.jsonl"),
    apiKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    inputDir: projectName is not null && Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "input", projectName))
        ? Path.Combine(Directory.GetCurrentDirectory(), "input", projectName) : null);

Func<string, string> Prompt(bool noInteractive) => noInteractive
    ? q => throw new InvalidOperationException($"--no-interactive but a prompt is required: {q}")
    : q => { Console.Write(q); return Console.ReadLine() ?? ""; };

// ---- logs/global-errors.jsonl: catch-all for exceptions with no project context yet -----
// (spec resolution itself failing, GenesisRoot() not finding templates/, etc.) System.CommandLine's
// own default pipeline already has exception-handling middleware (UseExceptionHandler) that
// intercepts and prints "Unhandled exception: ..." WITHOUT re-throwing past InvokeAsync - so this
// must be called from INSIDE each handler's own catch, not just wrapped around InvokeAsync itself,
// or it would never actually run. Best-effort: never let a logging failure mask the real exception.
void LogGlobalError(Exception ex)
{
    try
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "logs", "global-errors.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var entry = System.Text.Json.JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow,
            args = string.Join(' ', args),
            message = ex.Message,
            detail = ex.ToString(),
        });
        File.AppendAllText(path, entry + Environment.NewLine);
    }
    catch { /* best-effort */ }
}

// ---- Interactive spec/seed picking ----------------------------------------
// Stefan: "I may have multiple config json and multiple seeds, I want to choose
// which one every time in the CLI." Both pickers are pure add-ons: an explicit
// --spec always wins with zero prompting (scripting/automation stay untouched),
// and both only ever activate when there's genuinely more than one candidate -
// a single-spec, single-seed repo behaves exactly as before.

// Detected via the raw args array (not System.CommandLine's option-result API)
// so this doesn't depend on exactly which "was this explicit vs default" surface
// this System.CommandLine beta version exposes.
FileInfo ResolveSpecInteractively(FileInfo fallback, bool noInteractive)
{
    if (noInteractive || args.Contains("--spec")) return fallback;
    var candidates = InteractiveSelection.FindCandidates(
        new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "fixtures"),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas"),
        },
        "project-spec*.json")
        .Where(p => !p.EndsWith(".lock.json", StringComparison.OrdinalIgnoreCase)).ToList();
    // schemas/ archives use a "<project>-schema.json" name (see ArchiveCompletedProject), not
    // "project-spec*.json" - include those too so an archived spec stays pickable.
    candidates.AddRange(InteractiveSelection.FindCandidates(
        new[] { Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas") }, "*-schema.json"));
    candidates = candidates.Distinct().OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();

    if (candidates.Count == 0) return fallback; // nothing found anywhere: keep the original default behavior
    if (candidates.Count == 1) return new FileInfo(candidates[0]); // exactly one: use it, no need to prompt

    Console.Write(InteractiveSelection.FormatMenu(candidates, "Multiple project specs found:"));
    var chosen = InteractiveSelection.ParseSelection(Console.ReadLine(), candidates);
    if (chosen is null) { Console.WriteLine("Invalid selection - using default."); return fallback; }
    return new FileInfo(chosen);
}

// Only meaningful for the seed stage. Lets Stefan keep several candidate seed-response
// files under seeds/ and pick one each run; the chosen file is copied to the exact path
// LlmBridge reads (prompts/in/seed-response.json). Deletes any stale Data/seed-data.json
// in the target project first - otherwise SeedStage's own "reuse existing valid seed
// data" check (see SeedStage.RunAsync) would win and the fresh choice would silently do
// nothing.
void ResolveSeedInteractively(StageContext builtCtx, bool noInteractive)
{
    if (noInteractive) return;
    var candidates = InteractiveSelection.FindCandidates(
        new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "seeds"),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "seeds"),
        },
        "*.json");
    // Deliberately NOT auto-applying a sole candidate here (unlike the spec picker): a missing
    // spec is a hard crash, but a missing "choice" here just means SeedStage's own "reuse
    // existing valid seed data" logic handles it - auto-copying + deleting Data/seed-data.json
    // on every run with exactly one seeds/ file would force a needless reseed every time.
    if (candidates.Count < 2) return;

    Console.Write(InteractiveSelection.FormatMenu(candidates, "Multiple seed-response files found:"));
    var chosen = InteractiveSelection.ParseSelection(Console.ReadLine(), candidates);
    if (chosen is null) { Console.WriteLine("Invalid selection - leaving existing seed data/response untouched."); return; }

    var promptsIn = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in");
    Directory.CreateDirectory(promptsIn);
    File.Copy(chosen, Path.Combine(promptsIn, "seed-response.json"), overwrite: true);

    var seedDataPath = Path.Combine(builtCtx.ProjectDir, "Data", "seed-data.json");
    if (File.Exists(seedDataPath)) File.Delete(seedDataPath);
    Console.WriteLine($"Using {InteractiveSelection.RelativeOrFull(chosen)} for this run's seed data.");
}

// ---- logs/ project registry: maps every project to the spec + seed it was run with -----
// logs/registry.jsonl = full append-only history (one line per invocation, never rewritten).
// logs/registry.json  = quick-lookup snapshot, keyed by project name, always the LATEST run.
// Best-effort: a registry-write failure must never break the pipeline itself.
void RecordProjectRegistry(FileInfo spec, DirectoryInfo outDir, StageContext builtCtx)
{
    try
    {
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(logsDir);
        var seedResponsePath = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in", "seed-response.json");
        var projectName = builtCtx.Spec.Meta.ProjectName;
        var record = new
        {
            ts = DateTimeOffset.UtcNow,
            project = projectName,
            specPath = spec.FullName,
            seedResponsePath = File.Exists(seedResponsePath) ? seedResponsePath : null,
            outDir = outDir.FullName,
        };

        File.AppendAllText(Path.Combine(logsDir, "registry.jsonl"),
            System.Text.Json.JsonSerializer.Serialize(record) + Environment.NewLine);

        var snapshotPath = Path.Combine(logsDir, "registry.json");
        var snapshot = File.Exists(snapshotPath)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(snapshotPath)) ?? new()
            : new Dictionary<string, object>();
        snapshot[projectName] = record;
        File.WriteAllText(snapshotPath,
            System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    catch { /* registry is best-effort, never blocks the pipeline */ }
}

// ---- prompts/out/schemas + prompts/out/seeds: permanent per-project history ---------
// Copies (not moves) the spec + lock + seed into the archive folders after a green run.
// The originals are left untouched so a successful run never deletes the canonical dev
// fixture. Both search paths above already include these folders, so the archived copy
// stays selectable in later runs. Overwrites any previous archive entry for the same
// project name (matches logs/registry.json's own "keep the latest" convention).
// Best-effort: never let archival failure mask a real successful run.
void ArchiveCompletedProject(FileInfo spec, StageContext lockCtx)
{
    try
    {
        var projectName = lockCtx.Spec.Meta.ProjectName.ToLowerInvariant();
        var schemasDir = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas");
        var seedsDir = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "seeds");
        Directory.CreateDirectory(schemasDir);
        Directory.CreateDirectory(seedsDir);

        if (spec.Exists)
        {
            var destSpec = Path.Combine(schemasDir, $"{projectName}-schema.json");
            File.Copy(spec.FullName, destSpec, overwrite: true);

            var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
            if (File.Exists(lockPath))
            {
                var destLock = Path.Combine(schemasDir, $"{projectName}-schema.lock.json");
                File.Copy(lockPath, destLock, overwrite: true);
            }
        }

        var seedResponsePath = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in", "seed-response.json");
        if (File.Exists(seedResponsePath))
        {
            var destSeed = Path.Combine(seedsDir, $"{projectName}-seed.json");
            File.Copy(seedResponsePath, destSeed, overwrite: true);
        }

        Console.WriteLine($"Archived this run's spec/seed under prompts/out/schemas/ and prompts/out/seeds/ as \"{projectName}-*\".");
    }
    catch (Exception ex) { Console.WriteLine($"(archival skipped - {ex.Message})"); }
}

// ---- spec build / expand --------------------------------------------------
var specCmd = new Command("spec", "Spec authoring stages");
var buildCmd = new Command("build", "Draft spec from raw notes via LLM");
buildCmd.AddOption(notesOpt);
buildCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var notes = ctx.ParseResult.GetValueForOption(notesOpt) ?? throw new InvalidOperationException("--notes is required");
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        var genesisRoot = GenesisRoot();
        var stage = new SpecBuildStage(Llm(outDir), notes.FullName, spec.FullName,
            Path.Combine(genesisRoot, "spec.schema.json"), Prompt(noInt));
        // spec-build runs before a project exists: minimal synthetic context.
        var state = new StateStore(Path.Combine(outDir.FullName, ".specbuild", "state.json"));
        var dummy = new ProjectSpec();
        await RunStage(stage, new StageContext(dummy, VariationResolver.Resolve(dummy), outDir.FullName, genesisRoot, state, Console.Out));
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
var expandCmd = new Command("expand", "Expand features + write project-spec.lock.json");
expandCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        spec = ResolveSpecInteractively(spec, noInt);
        var genesisRoot = GenesisRoot();
        var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
        var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
        var stage = new SpecExpandStage(loader, spec.FullName, lockPath,
            Path.Combine(genesisRoot, "feature-templates"),
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is null && !File.Exists(Llm(outDir).ResponsePath("expand-review"))
                ? null : Llm(outDir), // review pass only when fillable; deterministic expansion always runs
            Prompt(noInt));
        await RunStage(stage, BuildContext(spec, outDir, useLock: false));
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
specCmd.AddCommand(buildCmd);
specCmd.AddCommand(expandCmd);
root.AddCommand(specCmd);

// ---- individual pipeline stages -------------------------------------------
void AddStageCommand(string name,
    Func<System.CommandLine.Invocation.InvocationContext, FileInfo, DirectoryInfo, string, IStage> factory)
{
    var cmd = new Command(name, $"Run the {name} stage");
    cmd.AddOption(imagePackOpt);
    cmd.AddOption(imageProviderOpt);
    cmd.SetHandler(async ctx =>
    {
        try
        {
            var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
            var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
            var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
            spec = ResolveSpecInteractively(spec, noInt);
            var builtCtx = BuildContext(spec, outDir, useLock: true);
            RecordProjectRegistry(spec, outDir, builtCtx);
            if (name == "seed") ResolveSeedInteractively(builtCtx, noInt);
            await RunStage(factory(ctx, spec, outDir, builtCtx.Spec.Meta.ProjectName), builtCtx);
        }
        catch (Exception ex) { LogGlobalError(ex); throw; }
    });
    root.AddCommand(cmd);
}

AddStageCommand("scaffold", (_, _, _, _) => new ScaffoldStage());
AddStageCommand("emit-models", (_, _, _, _) => new EmitModelsStage());
AddStageCommand("identity", (_, _, _, _) => new IdentityStage());
AddStageCommand("emit-controllers-views", (_, _, _, _) => new EmitControllersViewsStage());
AddStageCommand("seed", (ctx, _, outDir, projectName) => new SeedStage(
    Llm(outDir, projectName),
    ctx.ParseResult.GetValueForOption(imagePackOpt)?.FullName,
    ctx.ParseResult.GetValueForOption(imageProviderOpt)!));
AddStageCommand("verify", (_, _, _, _) => new VerifyStage());
AddStageCommand("report", (_, _, _, _) => new ReportStage());

// ---- run --all --------------------------------------------------------------
var runCmd = new Command("run", "Run the full pipeline");
var allOpt = new Option<bool>("--all", "Run all stages");
runCmd.AddOption(allOpt);
runCmd.AddOption(fromOpt);
runCmd.AddOption(imagePackOpt);
runCmd.AddOption(imageProviderOpt);
runCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var from = ctx.ParseResult.GetValueForOption(fromOpt);
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        spec = ResolveSpecInteractively(spec, noInt);
        var genesisRoot = GenesisRoot();
        var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
        var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";

        // Expansion always precedes the rest (idempotent); spec-build only with --notes flow.
        var expand = new SpecExpandStage(loader, spec.FullName, lockPath,
            Path.Combine(genesisRoot, "feature-templates"), null, Prompt(noInt));
        var preCtx = BuildContext(spec, outDir, useLock: false);
        if (!preCtx.State.IsDone("spec-expand")) { await expand.RunAsync(preCtx); preCtx.State.MarkDone("spec-expand"); }

        var lockCtx = BuildContext(spec, outDir, useLock: true);
        RecordProjectRegistry(spec, outDir, lockCtx);
        ResolveSeedInteractively(lockCtx, noInt);
        var stages = new IStage[]
        {
            new ScaffoldStage(), new EmitModelsStage(), new IdentityStage(), new EmitControllersViewsStage(),
            new SeedStage(Llm(outDir, lockCtx.Spec.Meta.ProjectName), ctx.ParseResult.GetValueForOption(imagePackOpt)?.FullName,
                ctx.ParseResult.GetValueForOption(imageProviderOpt)!),
            new VerifyStage(), new ReportStage(),
        };
        try
        {
            await new PipelineRunner(stages).RunAllAsync(lockCtx, from);
            PrintCompletionBanner(lockCtx);
            ArchiveCompletedProject(spec, lockCtx);
        }
        catch (LlmBridge.LlmPendingException e) { Console.WriteLine(e.Message); Environment.ExitCode = 3; }
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
root.AddCommand(runCmd);

// C2: after a full green run, print the ДПС-НН banner with seeded credentials + local URL.
static void PrintCompletionBanner(StageContext ctx)
{
    string? url = null;
    var lsPath = Path.Combine(ctx.ProjectDir, "Properties", "launchSettings.json");
    if (File.Exists(lsPath))
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(lsPath));
            var profiles = doc.RootElement.GetProperty("profiles");
            foreach (var profile in profiles.EnumerateObject())
                if (profile.Value.TryGetProperty("applicationUrl", out var au))
                {
                    url = au.GetString()?.Split(';').FirstOrDefault(u => u.StartsWith("http://")) ?? au.GetString();
                    break;
                }
        }
        catch { /* banner is best-effort */ }
    }

    string? dbName = null;
    var appsettingsPath = Path.Combine(ctx.ProjectDir, "appsettings.json");
    if (File.Exists(appsettingsPath))
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appsettingsPath));
            var csKey = $"{ctx.Spec.Meta.ProjectName}ContextConnection";
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csObj) &&
                csObj.TryGetProperty(csKey, out var csVal))
            {
                var m = System.Text.RegularExpressions.Regex.Match(csVal.GetString() ?? "", @"Database=([^;]+)");
                if (m.Success) dbName = m.Groups[1].Value;
            }
        }
        catch { /* banner is best-effort */ }
    }

    var users = ctx.Spec.Identity?.SeedUsers?.Users ?? [];
    Console.WriteLine();
    Console.WriteLine("========================================================");
    Console.WriteLine("  ДПС-НН - проектът е генериран успешно");
    Console.WriteLine("========================================================");
    if (url is not null) Console.WriteLine($"  URL:    {url}");
    if (dbName is not null) Console.WriteLine($"  БД:     {dbName}  (уникално име - виж appsettings.json)");
    Console.WriteLine($"  Старт:  cd {Path.Combine(ctx.WorkDir, ctx.Spec.Meta.ProjectName)} && dotnet run");
    Console.WriteLine("  --------------------------------------------------------");
    foreach (var u in users)
        Console.WriteLine($"  {u.Role,-16} {u.Email,-28} {u.Password}");
    Console.WriteLine("========================================================");
    Console.WriteLine();
}

static async Task RunStage(IStage stage, StageContext ctx)
{
    try
    {
        await stage.RunAsync(ctx);
        ctx.State.MarkDone(stage.Name);
    }
    catch (LlmBridge.LlmPendingException e)
    {
        Console.WriteLine(e.Message);
        Environment.ExitCode = 3; // distinct exit code: "waiting for manual LLM response"
    }
    catch (Exception ex)
    {
        ctx.Error($"{stage.Name} FAILED: {ex.Message}\n{ex}");
        throw;
    }
}

try
{
    return await root.InvokeAsync(args);
}
catch (Exception ex)
{
    // Defense-in-depth: System.CommandLine's own UseExceptionHandler middleware (visible in
    // the stack trace as "Unhandled exception: ...") normally intercepts handler exceptions
    // before they'd ever reach here - the REAL logging happens inside each handler's own
    // catch below. This only fires for something that somehow escapes that middleware too.
    LogGlobalError(ex);
    throw;
}
