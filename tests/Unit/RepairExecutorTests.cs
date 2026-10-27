using System.Security.Cryptography;
using System.Text;
using AgentNetDoctor.Core.Repair;

namespace AgentNetDoctor.UnitTests;

public sealed class RepairExecutorTests
{
    [Fact]
    public async Task CancellationDuringBackupStopsBeforeMutation()
    {
        using var cancellation = new CancellationTokenSource();
        var target = new MemoryTarget();
        var plan = Plan(target);
        var engine = new RepairExecutor(target, new MemoryBackup { OnSave = cancellation.Cancel });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ApplyAsync(plan,
            RepairPlanConfirmation.FromReviewedPlan(plan), cancellation.Token));
        Assert.Equal("original", Encoding.UTF8.GetString(target.Bytes));
    }
    [Fact]
    public async Task ChangedTargetAfterPreviewPreventsBackupAndWrite()
    {
        var target = new MemoryTarget();
        var plan = Plan(target);
        target.Bytes = Encoding.UTF8.GetBytes("external-change");
        var backup = new MemoryBackup();
        var record = await new RepairExecutor(target, backup).ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None);
        Assert.Equal(RepairState.Conflict, record.State);
        Assert.Equal(0, backup.Saves);
        Assert.Equal("external-change", Encoding.UTF8.GetString(target.Bytes));
    }

    [Fact]
    public async Task FailedBackupOrWrongConfirmationCannotWrite()
    {
        var target = new MemoryTarget();
        var plan = Plan(target);
        var engine = new RepairExecutor(target, new MemoryBackup { Fail = true });
        var rejected = await engine.ApplyAsync(plan, new("wrong", "wrong"), CancellationToken.None);
        Assert.Equal(RepairState.Rejected, rejected.State);
        await Assert.ThrowsAsync<IOException>(() => engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None));
        Assert.Equal("original", Encoding.UTF8.GetString(target.Bytes));
    }

    [Fact]
    public async Task ChangeDuringBackupPreventsWriteAndRollbackNeverOverwritesExternalChange()
    {
        var target = new MemoryTarget();
        var plan = Plan(target);
        var backup = new MemoryBackup { OnSave = () => target.Bytes = Encoding.UTF8.GetBytes("external-change") };
        var engine = new RepairExecutor(target, backup);
        Assert.Equal(RepairState.Conflict, (await engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None)).State);
        target = new MemoryTarget();
        plan = Plan(target);
        backup = new MemoryBackup();
        engine = new RepairExecutor(target, backup);
        var applied = await engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None);
        Assert.Equal(RepairState.AppliedNeedsVerification, applied.State);
        target.Bytes = Encoding.UTF8.GetBytes("external-change");
        Assert.Equal(RepairState.Conflict, await engine.RollbackAsync(applied, CancellationToken.None));
        Assert.Equal("external-change", Encoding.UTF8.GetString(target.Bytes));
    }

    [Fact]
    public async Task UnchangedTargetCanRestoreExactOriginal()
    {
        var target = new MemoryTarget();
        var plan = Plan(target);
        var engine = new RepairExecutor(target, new MemoryBackup());
        var record = await engine.ApplyAsync(plan, RepairPlanConfirmation.FromReviewedPlan(plan), CancellationToken.None);
        Assert.Equal(RepairState.RolledBackNeedsVerification, await engine.RollbackAsync(record, CancellationToken.None));
        Assert.Equal("original", Encoding.UTF8.GetString(target.Bytes));
    }

    private static RepairPlan Plan(MemoryTarget target) => new("synthetic-plan", ActionKind.PatchAppFields, "synthetic", "fixture", "1", target.Hash,
        [new("http.proxy", "", "http://127.0.0.1:8080")], "fixture", "fixture", "fixture");

    private sealed class MemoryTarget : IRepairTargetStore
    {
        public byte[] Bytes = Encoding.UTF8.GetBytes("original");
        public string Hash => Convert.ToHexString(SHA256.HashData(Bytes));
        public bool Supports(RepairPlan plan) => plan.TargetId == "fixture";
        public Task<string> ReadFingerprintAsync(RepairPlan plan, CancellationToken cancellationToken) => Task.FromResult(Hash);
        public Task<byte[]> ReadOriginalAsync(RepairPlan plan, CancellationToken cancellationToken) => Task.FromResult(Bytes.ToArray());
        public Task<string?> ApplyIfMatchesAsync(RepairPlan plan, CancellationToken cancellationToken)
        {
            if (Hash != plan.BeforeFingerprint) return Task.FromResult<string?>(null);
            Bytes = Encoding.UTF8.GetBytes("patched"); return Task.FromResult<string?>(Hash);
        }
        public Task<bool> RestoreIfMatchesAsync(RepairPlan plan, string expectedFingerprint, byte[] original, CancellationToken cancellationToken)
        {
            if (Hash != expectedFingerprint) return Task.FromResult(false);
            Bytes = original.ToArray(); return Task.FromResult(true);
        }
    }

    private sealed class MemoryBackup : IProtectedBackupStore
    {
        public int Saves;
        public bool Fail;
        public Action? OnSave;
        private byte[] bytes = [];
        public Task<string> SaveAsync(string targetId, byte[] original, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("synthetic protection failure");
            Saves++; bytes = original.ToArray(); OnSave?.Invoke(); return Task.FromResult("synthetic-backup");
        }
        public Task<byte[]> ReadAsync(string backupId, string targetId, CancellationToken cancellationToken) => Task.FromResult(bytes.ToArray());
    }
}
