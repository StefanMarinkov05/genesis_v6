using System.Collections.Concurrent;

namespace GenesisV6.Core.Pipeline;

/// <summary>
/// Bounded, named-worker parallel executor for the pipeline's pure-emission fan-out
/// (GitHub issue #5 — "async pipeline with Bulgarian thread names").
///
/// WHY it exists, and WHY it is deliberately confined to a couple of call sites:
/// the 9 pipeline stages are a strict linear chain — each consumes the previous stage's files
/// and ends with a `dotnet build` (or an external `dotnet`/`ef`/`aspnet-codegenerator`) against
/// ONE shared generated project. No two STAGES, and no two builds, may overlap on that project
/// without corrupting it. The ONLY race-free parallelism is WITHIN the two pure-emission stages:
/// rendering a Scriban template and writing it to a DISTINCT file path. Those work items share no
/// mutable state, spawn no external process, and never read a file another item writes — so
/// running up to five at once is safe. Every build gate stays serial, AFTER RunAsync returns.
///
/// Kleppmann lens: this is a maintainability/throughput win, NOT a scalability play. The work set
/// is a few dozen files, so five lanes is already more than enough — more lanes would only add
/// scheduling contention for files that render in milliseconds. We pick the smallest degree of
/// parallelism that actually helps and stop there.
/// </summary>
public static class ParallelEmit
{
    /// <summary>
    /// The five workers on the generator's "crew". Their names are printed in the logs so a
    /// parallel run reads like a team splitting the work (issue #5). Order is fixed — lane 0 is
    /// always Кольо, lane 1 Наско, and so on — so the output is stable and readable across runs.
    /// </summary>
    public static readonly string[] WorkerNames = ["Кольо", "Наско", "Иван", "Мишо", "Киро"];

    /// <summary>
    /// The name of the worker currently running the calling code, or <c>null</c> on the
    /// orchestrator thread. AsyncLocal (not ThreadLocal) because each work item is async and may
    /// resume on a different thread-pool thread after an <c>await</c> — the value is captured into
    /// the item's own ExecutionContext when its lane starts it, so <see cref="StageContext.Info"/>
    /// can tag every line with the right name regardless of thread hops. It reads <c>null</c>
    /// outside a parallel region, so ordinary sequential logging is completely unchanged.
    /// </summary>
    internal static readonly AsyncLocal<string?> CurrentWorker = new();

    /// <summary>
    /// Run <paramref name="items"/> across up to five named lanes and complete when all have
    /// finished. Lanes pull from a shared queue rather than getting a fixed slice up front, so a
    /// lane that draws light items keeps helping instead of idling while another grinds a heavy
    /// one (dynamic load-balancing — better than static chunking when render costs vary).
    ///
    /// Failure semantics match the sequential path this replaces: if any item throws, RunAsync
    /// throws (the exception surfaces through <see cref="Task.WhenAll(Task[])"/>). Sibling lanes
    /// may finish draining their in-flight items first — harmless, because every item writes a
    /// distinct file and the stage is idempotent on re-run — but the pipeline still stops loud.
    /// </summary>
    public static async Task RunAsync(IReadOnlyCollection<(string Label, Func<Task> Work)> items)
    {
        if (items.Count == 0) return;

        var queue = new ConcurrentQueue<(string Label, Func<Task> Work)>(items);
        var laneCount = Math.Min(WorkerNames.Length, items.Count);

        var lanes = new Task[laneCount];
        for (var i = 0; i < laneCount; i++)
        {
            var workerName = WorkerNames[i];
            lanes[i] = Task.Run(async () =>
            {
                // Set INSIDE this lane's own task body: an AsyncLocal write mutates only the
                // current execution context and does NOT flow back up to the parent, so lanes
                // never see each other's names and the orchestrator context stays null.
                CurrentWorker.Value = workerName;
                while (queue.TryDequeue(out var item))
                    await item.Work().ConfigureAwait(false);
            });
        }

        await Task.WhenAll(lanes).ConfigureAwait(false);
    }
}
