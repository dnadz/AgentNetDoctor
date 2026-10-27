using AgentNetDoctor.Core.Repair;
using AgentNetDoctor.Core.Adapters;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AgentNetDoctor.Windows.Repair;

// Registrations are compiled/adapter-owned support decisions, never deserialized from a log or repair request.
public sealed record VerifiedFileTarget(string Id, string ToolId, string Version, string ConfigurationPath,
    Func<string?> CurrentVersion, Func<bool> IsStopped);

public sealed class FileRepairTargetStore(IReadOnlyList<VerifiedFileTarget> registrations,
    IProtectedBackupStore backups, IRepairJournal journal) : IRepairTargetStore
{
    private sealed record Binding(string BackupId, string PlanDigest);
    private readonly IReadOnlyDictionary<string, VerifiedFileTarget> targets = registrations.ToDictionary(target => target.Id, StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Binding> bindings = new(StringComparer.Ordinal);

    public bool Supports(RepairPlan plan) =>
        plan.Kind == ActionKind.PatchAppFields && Guid.TryParseExact(plan.Id, "N", out _) &&
        plan.Changes.Count == 1 && plan.Changes[0].Field == "http.proxy" &&
        targets.TryGetValue(plan.TargetId, out var target) && target.ToolId == "antigravity" && plan.ToolId == target.ToolId &&
        !string.IsNullOrWhiteSpace(target.Version) && plan.TargetVersion == target.Version && target.CurrentVersion() == target.Version &&
        target.IsStopped() && WindowsPathPolicy.IsLocalDriveSyntax(target.ConfigurationPath);

    public async Task<string> ReadFingerprintAsync(RepairPlan plan, CancellationToken cancellationToken)
    {
        var bytes = await ReadOriginalAsync(plan, cancellationToken);
        try { return Hash(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public async Task<byte[]> ReadOriginalAsync(RepairPlan plan, CancellationToken cancellationToken)
    {
        var path = Resolve(plan);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await ReadBytes(file, cancellationToken);
    }

    public async Task BindRecoveryAsync(RepairPlan plan, string backupId, CancellationToken cancellationToken)
    {
        Resolve(plan);
        var original = await backups.ReadAsync(backupId, plan.TargetId, cancellationToken);
        try
        {
            if (Hash(original) != plan.BeforeFingerprint) throw new InvalidDataException("备份不匹配原始状态");
            cancellationToken.ThrowIfCancellationRequested();
            bindings[plan.Id] = new(backupId, RepairPlanConfirmation.Digest(plan));
        }
        finally { CryptographicOperations.ZeroMemory(original); }
    }

    public async Task<string?> ApplyIfMatchesAsync(RepairPlan plan, CancellationToken cancellationToken)
    {
        plan = Freeze(plan);
        var binding = TakeBinding(plan);
        await using var file = OpenExclusive(Resolve(plan));
        var original = await ReadBytes(file, cancellationToken);
        try
        {
            if (Hash(original) != plan.BeforeFingerprint) return null;
            var changed = ProxyFieldPatcher.Patch(original, plan);
            try
            {
                var expected = Hash(changed);
                await Mutate(file, plan, binding, MutationDirection.Apply, plan.BeforeFingerprint, expected, changed, cancellationToken);
                return expected;
            }
            finally { CryptographicOperations.ZeroMemory(changed); }
        }
        finally { CryptographicOperations.ZeroMemory(original); }
    }

    public async Task<bool> RestoreIfMatchesAsync(RepairPlan plan, string expectedFingerprint, byte[] original, CancellationToken cancellationToken)
    {
        plan = Freeze(plan);
        var binding = TakeBinding(plan);
        if (Hash(original) != plan.BeforeFingerprint) throw new InvalidDataException("恢复内容不匹配原始状态");
        await using var file = OpenExclusive(Resolve(plan));
        var current = await ReadBytes(file, cancellationToken);
        try
        {
            if (Hash(current) != expectedFingerprint) return false;
            await Mutate(file, plan, binding, MutationDirection.Rollback, expectedFingerprint, plan.BeforeFingerprint, original, cancellationToken);
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(current); }
    }

    private async Task Mutate(FileStream file, RepairPlan plan, Binding binding, MutationDirection direction,
        string before, string after, byte[] bytes, CancellationToken cancellationToken)
    {
        var entry = new RepairJournalEntry(plan.Id, plan, binding.BackupId, direction, before, after, JournalStage.Prepared);
        await journal.SaveAsync(entry, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Supports(plan)) throw new InvalidOperationException("目标状态已变化，未开始写入");
        // Once mutation starts, finish this bounded write and recovery record; cancellation must not split the file.
        file.Position = 0;
        await file.WriteAsync(bytes, CancellationToken.None);
        file.SetLength(bytes.Length);
        await file.FlushAsync(CancellationToken.None);
        file.Flush(true);
        file.Position = 0;
        var readback = await ReadBytes(file, CancellationToken.None);
        try { if (Hash(readback) != after) throw new IOException("写入后核对失败，请检查恢复记录"); }
        finally { CryptographicOperations.ZeroMemory(readback); }
        await journal.SaveAsync(entry with { Stage = JournalStage.Completed }, CancellationToken.None);
    }

    private string Resolve(RepairPlan plan)
    {
        if (!Supports(plan)) throw new InvalidOperationException("目标未注册、版本未核实或尚未退出");
        return LocalFileGuard.Validate(targets[plan.TargetId].ConfigurationPath);
    }

    private Binding TakeBinding(RepairPlan plan)
    {
        if (!bindings.TryRemove(plan.Id, out var binding) || binding.PlanDigest != RepairPlanConfirmation.Digest(plan))
            throw new InvalidOperationException("缺少匹配的受保护恢复上下文");
        return binding;
    }

    private static FileStream OpenExclusive(string path) => new(path, FileMode.Open, FileAccess.ReadWrite,
        FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
    private static async Task<byte[]> ReadBytes(FileStream file, CancellationToken cancellationToken)
    {
        if (file.Length is < 0 or > 1024 * 1024) throw new InvalidDataException("配置超过大小限制");
        var bytes = new byte[(int)file.Length];
        try { await file.ReadExactlyAsync(bytes, cancellationToken); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static RepairPlan Freeze(RepairPlan plan) => plan with { Changes = Array.AsReadOnly(plan.Changes.ToArray()) };
}
