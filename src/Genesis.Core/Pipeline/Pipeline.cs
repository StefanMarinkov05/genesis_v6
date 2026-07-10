using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Variation;

namespace GenesisV6.Core.Pipeline;

/// <summary>
/// Everything a stage needs. WorkDir = repo root of the GENERATED project
/// (e.g. out/FaceNote); GenesisRoot = where genesis' own templates live.
/// Spec here is ALWAYS the lock (expanded) spec for stages >= scaffold.
/// </summary>
public sealed record StageContext(
    ProjectSpec Spec,
    ResolvedVariation Variation,
    string WorkDir,
    string GenesisRoot,
    StateStore State,
    TextWriter Log)
{
    public string ProjectDir => Path.Combine(WorkDir, Spec.Meta.ProjectName);
    public string Template(string relative) => Path.Combine(GenesisRoot, "templates", relative);

    // logs/<Project>/actions.log.jsonl - a per-project, LEVELED action log, separate from
    // the generated project's own out/<Project>/dps-nn.log.jsonl (which only records stage
    // start/end/error timings, see PipelineRunner). Rooted at cwd (not WorkDir), so every
    // project this tool has ever touched shows up under one logs/ folder regardless of
    // where --out pointed for that run. Best-effort: a logging failure must never break
    // the pipeline itself.
    private string ActionLogPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "logs", Spec.Meta.ProjectName, "actions.log.jsonl");

    // One process-wide lock guarding BOTH sinks. The console (a single shared TextWriter) and the
    // action-log file (unsynchronized File.AppendAllText) are global resources, so a global lock is
    // the right scope: it serializes every log line across the parallel emission lanes (issue #5)
    // so console output never interleaves mid-line and concurrent file appends can't corrupt each
    // other. Uncontended and negligible on the sequential path.
    private static readonly object LogLock = new();

    public void Info(string msg) { var m = Tag(msg); lock (LogLock) { Log.WriteLine($"[dps-nn] {m}"); } AppendActionLog("info", m); }

    /// <summary>File-only (no console spam) - for deeper diagnosis without cluttering stdout.</summary>
    public void Debug(string msg) => AppendActionLog("debug", Tag(msg));

    /// <summary>Console + action log, distinct level - for actual failures, not routine progress.</summary>
    public void Error(string msg) { var m = Tag(msg); lock (LogLock) { Log.WriteLine($"[dps-nn] [ERROR] {m}"); } AppendActionLog("error", m); }

    // Prefix each line with the parallel worker's Bulgarian name when one is running this code
    // (issue #5), e.g. "[Кольо] wrote Models/Product.cs". Null outside a parallel region, so
    // sequential logging is byte-for-byte unchanged.
    private static string Tag(string msg) =>
        ParallelEmit.CurrentWorker.Value is { } worker ? $"[{worker}] {msg}" : msg;

    private void AppendActionLog(string level, string msg)
    {
        try
        {
            var path = ActionLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var entry = JsonSerializer.Serialize(new { ts = DateTimeOffset.UtcNow, level, msg });
            lock (LogLock) { File.AppendAllText(path, entry + Environment.NewLine); }
        }
        catch { /* action log is best-effort */ }
    }
}

public interface IStage
{
    string Name { get; }
    /// <summary>Idempotent: safe to re-run; must detect already-done work.</summary>
    Task RunAsync(StageContext ctx, CancellationToken ct = default);
}

/// <summary>
/// Pipeline runtime state - lives in plan/state.json NEXT TO the generated
/// project, never inside the spec (ADR-001). Enables `genesis run --all`
/// resumption after any failure.
/// </summary>
public sealed class StateStore(string path)
{
    public sealed class StateModel
    {
        [JsonPropertyName("completedStages")] public List<string> CompletedStages { get; set; } = [];
        [JsonPropertyName("lastRun")] public DateTimeOffset? LastRun { get; set; }
        [JsonPropertyName("specHash")] public string? SpecHash { get; set; }
    }

    public StateModel Model { get; private set; } = Load(path);

    private static StateModel Load(string p) =>
        File.Exists(p)
            ? JsonSerializer.Deserialize<StateModel>(File.ReadAllText(p)) ?? new()
            : new();

    public bool IsDone(string stage) => Model.CompletedStages.Contains(stage);

    public void MarkDone(string stage)
    {
        if (!Model.CompletedStages.Contains(stage)) Model.CompletedStages.Add(stage);
        Model.LastRun = DateTimeOffset.UtcNow;
        Save();
    }

    public void Invalidate(string fromStage, IReadOnlyList<string> order)
    {
        var idx = order.ToList().IndexOf(fromStage);
        if (idx < 0) return;
        Model.CompletedStages.RemoveAll(s => order.ToList().IndexOf(s) >= idx);
        Save();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(Model, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class PipelineRunner(IReadOnlyList<IStage> stages)
{
    public static readonly string[] CanonicalOrder =
        ["spec-build", "spec-expand", "scaffold", "emit-models", "identity", "emit-controllers-views", "seed", "verify", "report"];

    public async Task RunAllAsync(StageContext ctx, string? fromStage = null, CancellationToken ct = default)
    {
        var started = fromStage is null;
        var totalSw = Stopwatch.StartNew();
        var timings = new List<(string Stage, long Ms, string Outcome)>();
        var logPath = Path.Combine(ctx.WorkDir, ctx.Spec.Meta.ProjectName, "dps-nn.log.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        foreach (var stage in stages)
        {
            if (!started && stage.Name == fromStage) started = true;
            if (!started) continue;
            if (fromStage is null && ctx.State.IsDone(stage.Name))
            {
                ctx.Info($"skip {stage.Name} (already completed - state file)");
                timings.Add((stage.Name, 0, "skipped"));
                continue;
            }
            ctx.Info($"=== stage: {stage.Name} ===");
            var sw = Stopwatch.StartNew();
            AppendLog(logPath, stage.Name, "start", 0, null);
            try
            {
                await stage.RunAsync(ctx, ct);
                sw.Stop();
                ctx.State.MarkDone(stage.Name);
                timings.Add((stage.Name, sw.ElapsedMilliseconds, "ok"));
                AppendLog(logPath, stage.Name, "end", sw.ElapsedMilliseconds, null);
                ctx.Info($"    {stage.Name} done in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                timings.Add((stage.Name, sw.ElapsedMilliseconds, "FAILED"));
                AppendLog(logPath, stage.Name, "error", sw.ElapsedMilliseconds, ex.Message);
                // Full detail (not just ex.Message) - so it's still there in logs/ even after
                // the console scrollback is gone. AppendActionLog is best-effort; a logging
                // failure here must never mask the real exception below.
                ctx.Error($"{stage.Name} FAILED: {ex.Message}\n{ex}");
                throw;
            }
        }
        totalSw.Stop();

        // C1: end-of-run summary table with total runtime. The TOTAL is shown in raw ms AND a
        // human-readable minutes/seconds form - a full generate runs into several minutes, and
        // "385593 ms" is hard to read at a glance ("6m 25s / 385.6s" is not).
        ctx.Info("");
        ctx.Info("=== stage timings ===");
        foreach (var (st, ms, outcome) in timings)
            ctx.Info($"    {st,-26} {ms,7} ms   {outcome}");
        ctx.Info($"    {"TOTAL",-26} {totalSw.ElapsedMilliseconds,7} ms   ({HumanDuration(totalSw.ElapsedMilliseconds)})");
    }

    /// <summary>
    /// A duration in milliseconds as a glanceable string: "6m 25s / 385.6s" once it passes a
    /// minute, else just "42.3s". Always includes total seconds so the two views agree.
    /// </summary>
    internal static string HumanDuration(long ms)
    {
        // InvariantCulture so the decimal separator is always "." regardless of the machine's
        // locale (this repo runs under a non-en locale) - the log must read the same everywhere.
        var totalSeconds = (ms / 1000.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalMinutes >= 1
            ? $"{(int)t.TotalMinutes}m {t.Seconds}s / {totalSeconds}s"
            : $"{totalSeconds}s";
    }

    private static void AppendLog(string path, string stage, string phase, long ms, string? error)
    {
        var entry = JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow,
            stage,
            phase,
            durationMs = ms,
            error,
        });
        File.AppendAllText(path, entry + "\n");
    }
}

/// <summary>
/// Persisted per-project DB lifecycle state in logs/&lt;Project&gt;/db.json.
/// Tracks the spec hash used when the DB was created and whether EF migrations
/// have been applied to it, so subsequent runs can reuse the same database
/// instead of creating a new timestamped one every time.
/// </summary>
public sealed class DbLifecycleRecord
{
    [System.Text.Json.Serialization.JsonPropertyName("specHash")] public string? SpecHash { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("dbName")] public string? DbName { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("migrationsApplied")] public bool MigrationsApplied { get; set; }
}

/// <summary>Thin wrapper over external `dotnet` invocations with fail-loud semantics.</summary>
public static class Proc
{
    public sealed record Result(int ExitCode, string StdOut, string StdErr)
    {
        public void ThrowIfFailed(string what)
        {
            if (ExitCode != 0)
                throw new InvalidOperationException($"{what} failed (exit {ExitCode}).\nSTDOUT:\n{StdOut}\nSTDERR:\n{StdErr}");
        }
    }

    public static async Task<Result> RunAsync(string fileName, string args, string workDir, CancellationToken ct = default,
        IDictionary<string, string?>? env = null, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (env is not null) foreach (var (k, v) in env) psi.Environment[k] = v;

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}");
        var so = p.StandardOutput.ReadToEndAsync(ct);
        var se = p.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException($"{fileName} {args} timed out after {timeout}.");
        }
        return new Result(p.ExitCode, await so, await se);
    }
}
