using System.Security.Cryptography;
using System.Text;
using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Repair;
using AgentNetDoctor.Windows.Repair;

namespace AgentNetDoctor.IntegrationTests;

public sealed class RepairRoundTripTests
{
    [Theory]
    [InlineData(JournalStage.Prepared, RecoveryOutcome.SourceStateObserved)]
    [InlineData(JournalStage.Completed, RecoveryOutcome.ExpectedStateObserved)]
    public async Task JournalFailureLeavesInspectableStateWithoutAutomaticOverwrite(JournalStage failAt, RecoveryOutcome expected)
    {
        var fixture = new RepairFixture();
        var backups = new ProtectedBackupStore(Path.Combine(fixture.Root, "fault-backups"));
        var persisted = new ProtectedRepairJournal(fixture.JournalRoot);
        var store = new FileRepairTargetStore([new("fixture", "antigravity", "synthetic-1", fixture.Configuration, () => "synthetic-1", () => true)],
            backups, new FailingJournal(persisted, failAt));
        var plan = fixture.Plan();
        await Assert.ThrowsAsync<IOException>(() => new RepairExecutor(store, backups).ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None));
        var entry = await new ProtectedRepairJournal(fixture.JournalRoot).ReadAsync(plan.Id, CancellationToken.None);
        Assert.Equal(JournalStage.Prepared, entry.Stage);
        Assert.Equal(expected, (await RecoveryInspector.InspectAsync(entry, fixture.Store, CancellationToken.None)).Outcome);
        var observed = await File.ReadAllBytesAsync(fixture.Configuration);
        Assert.Equal(failAt == JournalStage.Prepared ? fixture.Original : fixture.Expected, observed);
        await File.WriteAllTextAsync(fixture.Configuration, "external-or-incomplete");
        Assert.Equal(RecoveryOutcome.Conflict, (await RecoveryInspector.InspectAsync(entry, fixture.Store, CancellationToken.None)).Outcome);
        var record = new RepairRecord(plan, RepairState.AppliedNeedsVerification, entry.BackupId, entry.ExpectedFingerprint);
        Assert.Equal(RepairState.Conflict, await new RepairExecutor(store, backups).RollbackAsync(record, CancellationToken.None));
        Assert.Equal("external-or-incomplete", await File.ReadAllTextAsync(fixture.Configuration));
    }

    private sealed class FailingJournal(IRepairJournal inner, JournalStage failAt) : IRepairJournal
    {
        public async Task SaveAsync(RepairJournalEntry entry, CancellationToken cancellationToken)
        {
            if (entry.Stage == JournalStage.Completed && failAt == JournalStage.Completed) throw new IOException("synthetic completion failure");
            await inner.SaveAsync(entry, cancellationToken);
            if (entry.Stage == JournalStage.Prepared && failAt == JournalStage.Prepared) throw new IOException("synthetic pre-write failure");
        }
        public Task<RepairJournalEntry> ReadAsync(string id, CancellationToken cancellationToken) => inner.ReadAsync(id, cancellationToken);
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);
    }

    [Fact]
    public async Task ActualFileRoundTripPreservesUnrelatedBytesAndReloadableJournal()
    {
        var fixture = new RepairFixture();
        var plan = fixture.Plan();
        var applied = await fixture.Engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None);
        Assert.Equal(RepairState.AppliedNeedsVerification, applied.State);
        Assert.Equal(fixture.Expected, await File.ReadAllBytesAsync(fixture.Configuration));
        var journal = await new ProtectedRepairJournal(fixture.JournalRoot).ReadAsync(plan.Id, CancellationToken.None);
        Assert.Equal(JournalStage.Completed, journal.Stage);
        Assert.Equal(applied.AfterFingerprint, journal.ExpectedFingerprint);
        Assert.Equal(RepairState.RolledBackNeedsVerification, await fixture.Engine.RollbackAsync(applied, CancellationToken.None));
        Assert.Equal(fixture.Original, await File.ReadAllBytesAsync(fixture.Configuration));
    }

    [Fact]
    public async Task NewProcessVersionOrStalePreviewCannotWrite()
    {
        var fixture = new RepairFixture();
        var plan = fixture.Plan();
        fixture.Version = "changed-version";
        Assert.Equal(RepairState.Rejected, (await fixture.Engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None)).State);
        fixture.Version = "synthetic-1";
        fixture.Stopped = false;
        Assert.False(fixture.Store.Supports(plan));
        fixture.Stopped = true;
        await File.WriteAllTextAsync(fixture.Configuration, "external-change");
        Assert.Equal(RepairState.Conflict, (await fixture.Engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None)).State);
        Assert.Equal("external-change", await File.ReadAllTextAsync(fixture.Configuration));
    }

    [Fact]
    public async Task ExistingFileHandlePreventsMutationAndUnregisteredTargetCannotOpen()
    {
        var fixture = new RepairFixture();
        var plan = fixture.Plan();
        await using (var other = new FileStream(fixture.Configuration, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<IOException>(() => fixture.Engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None));
        Assert.Equal(fixture.Original, await File.ReadAllBytesAsync(fixture.Configuration));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.ReadOriginalAsync(plan with { TargetId = "outside" }, CancellationToken.None));
    }
}

internal sealed class RepairFixture
{
    public string Root { get; } = Path.Combine(AppContext.BaseDirectory, "synthetic-repair-" + Guid.NewGuid().ToString("N"));
    public string Configuration => Path.Combine(Root, "settings.json");
    public string JournalRoot => Path.Combine(Root, "journal");
    public string Version = "synthetic-1";
    public bool Stopped = true;
    public byte[] Original { get; } = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{ // synthetic fixture\r\n\"http.proxy\":\"http://127.0.0.1:8080\",\r\n\"unknown\":[1,2],\r\n}\r\n")).ToArray();
    public byte[] Expected => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Original.AsSpan(3)).Replace(":8080", ":9090"))).ToArray();
    public FileRepairTargetStore Store { get; }
    public RepairExecutor Engine { get; }

    public RepairFixture()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllBytes(Configuration, Original);
        var backups = new ProtectedBackupStore(Path.Combine(Root, "backups"));
        Store = new([new("fixture", "antigravity", "synthetic-1", Configuration, () => Version, () => Stopped)],
            backups, new ProtectedRepairJournal(JournalRoot));
        Engine = new(Store, backups);
    }

    public RepairPlan Plan() => RepairPlanner.PlanAppFields("antigravity", "synthetic-1", new(true, ["http.proxy"], "synthetic fixture"),
        "fixture", Convert.ToHexString(SHA256.HashData(Original)), [new("http.proxy", "http://127.0.0.1:8080", "http://127.0.0.1:9090")], false)!;
}
