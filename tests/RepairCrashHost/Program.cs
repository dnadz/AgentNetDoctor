using AgentNetDoctor.Core.Repair;
using AgentNetDoctor.Windows.Repair;
using System.Security.Cryptography;
using System.Text.Json;

// Test-only executable: never included in application packaging.
if (args.Length != 2) return 2;
var observe = args[0] == "observe";
if (!observe && args[1] is not ("before" or "after")) return 2;
var root = Path.GetFullPath(observe ? args[1] : args[0]);
if (!Path.GetFileName(root).StartsWith("synthetic-repair-", StringComparison.Ordinal) ||
    !File.Exists(Path.Combine(root, "owned-crash-fixture"))) return 3;
if (observe)
{
    var values = new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY" }.ToDictionary(key => key, Environment.GetEnvironmentVariable);
    await File.WriteAllTextAsync(Path.Combine(root, "observed.json"), JsonSerializer.Serialize(values));
    var deadline = System.Diagnostics.Stopwatch.StartNew();
    while (!File.Exists(Path.Combine(root, "release-child")))
    {
        if (deadline.Elapsed > TimeSpan.FromSeconds(20)) return 6;
        await Task.Delay(25);
    }
    return 0;
}
var path = Path.Combine(root, "settings.json");
var original = await File.ReadAllBytesAsync(path);
var backups = new ProtectedBackupStore(Path.Combine(root, "backups"));
var journal = new InterruptedJournal(new ProtectedRepairJournal(Path.Combine(root, "journal")), args[1]);
var targets = new FileRepairTargetStore([new("fixture", "antigravity", "synthetic-1", path, () => "synthetic-1", () => true)], backups, journal);
var plan = RepairPlanner.PlanAppFields("antigravity", "synthetic-1", new(true, ["http.proxy"], "synthetic fixture"),
    "fixture", Convert.ToHexString(SHA256.HashData(original)), [new("http.proxy", "http://127.0.0.1:8080", "http://127.0.0.1:9090")], false)!;
await new RepairExecutor(targets, backups).ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None);
return 0;

sealed class InterruptedJournal(IRepairJournal inner, string point) : IRepairJournal
{
    public async Task SaveAsync(RepairJournalEntry entry, CancellationToken cancellationToken)
    {
        // Exit skips the active mutation's completion path without invoking WER/FailFast.
        if (point == "after" && entry.Stage == JournalStage.Completed) Environment.Exit(74);
        await inner.SaveAsync(entry, cancellationToken);
        if (point == "before" && entry.Stage == JournalStage.Prepared) Environment.Exit(73);
    }
    public Task<RepairJournalEntry> ReadAsync(string id, CancellationToken cancellationToken) => inner.ReadAsync(id, cancellationToken);
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);
}
