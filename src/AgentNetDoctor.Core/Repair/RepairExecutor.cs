using System.Security.Cryptography;

namespace AgentNetDoctor.Core.Repair;

public interface IRepairTargetStore
{
    bool Supports(RepairPlan plan);
    Task<string> ReadFingerprintAsync(RepairPlan plan, CancellationToken cancellationToken);
    Task<byte[]> ReadOriginalAsync(RepairPlan plan, CancellationToken cancellationToken);
    // Production stores bind a validated protected backup before they can mutate; memory fixtures need no binding.
    Task BindRecoveryAsync(RepairPlan plan, string backupId, CancellationToken cancellationToken) => Task.CompletedTask;
    // Implementations must recheck under their write lock; comparison outside the mutation is insufficient.
    // Return the fingerprint of OUR written bytes under that lock, never a later unlocked read.
    Task<string?> ApplyIfMatchesAsync(RepairPlan plan, CancellationToken cancellationToken);
    Task<bool> RestoreIfMatchesAsync(RepairPlan plan, string expectedFingerprint, byte[] original, CancellationToken cancellationToken);
}

public enum RepairState { Rejected, Conflict, AppliedNeedsVerification, RolledBackNeedsVerification }
public sealed record RepairRecord(RepairPlan Plan, RepairState State, string? BackupId, string? AfterFingerprint);

public sealed class RepairExecutor(IRepairTargetStore targets, IProtectedBackupStore backups)
{
    public async Task<RepairRecord> ApplyAsync(RepairPlan plan, ConfirmedPlan confirmation, CancellationToken cancellationToken)
    {
        plan = plan with { Changes = Array.AsReadOnly(plan.Changes.ToArray()) };
        cancellationToken.ThrowIfCancellationRequested();
        if (!RepairPlanConfirmation.Matches(plan, confirmation) || !targets.Supports(plan))
            return new(plan, RepairState.Rejected, null, null);
        if (await targets.ReadFingerprintAsync(plan, cancellationToken) != plan.BeforeFingerprint)
            return new(plan, RepairState.Conflict, null, null);
        var original = await targets.ReadOriginalAsync(plan, cancellationToken);
        try
        {
            if (Fingerprint(original) != plan.BeforeFingerprint) return new(plan, RepairState.Conflict, null, null);
            var backupId = await backups.SaveAsync(plan.TargetId, original, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!targets.Supports(plan)) return new(plan, RepairState.Rejected, backupId, null);
            await targets.BindRecoveryAsync(plan, backupId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var after = await targets.ApplyIfMatchesAsync(plan, cancellationToken);
            return new(plan, after is null ? RepairState.Conflict : RepairState.AppliedNeedsVerification, backupId, after);
        }
        finally { CryptographicOperations.ZeroMemory(original); }
    }

    public async Task<RepairState> RollbackAsync(RepairRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (record.State != RepairState.AppliedNeedsVerification || record.BackupId is null || record.AfterFingerprint is null || !targets.Supports(record.Plan))
            return RepairState.Rejected;
        if (await targets.ReadFingerprintAsync(record.Plan, cancellationToken) != record.AfterFingerprint) return RepairState.Conflict;
        var original = await backups.ReadAsync(record.BackupId, record.Plan.TargetId, cancellationToken);
        try
        {
            if (Fingerprint(original) != record.Plan.BeforeFingerprint) throw new InvalidDataException("备份与计划原始状态不匹配");
            cancellationToken.ThrowIfCancellationRequested();
            await targets.BindRecoveryAsync(record.Plan, record.BackupId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await targets.RestoreIfMatchesAsync(record.Plan, record.AfterFingerprint, original, cancellationToken)
                ? RepairState.RolledBackNeedsVerification : RepairState.Conflict;
        }
        finally { CryptographicOperations.ZeroMemory(original); }
    }

    private static string Fingerprint(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}
