namespace AgentNetDoctor.Core.Repair;

public enum RecoveryOutcome { SourceStateObserved, ExpectedStateObserved, Conflict, Unavailable }
public sealed record RecoveryAssessment(string Id, string TargetId, JournalStage RecordedStage, RecoveryOutcome Outcome, string Explanation);

public static class RecoveryInspector
{
    public static async Task<RecoveryAssessment> InspectAsync(RepairJournalEntry entry, IRepairTargetStore targets, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!targets.Supports(entry.Plan)) return Result(RecoveryOutcome.Unavailable, "目标版本或运行状态不支持安全检查。");
            var current = await targets.ReadFingerprintAsync(entry.Plan, cancellationToken);
            if (current == entry.SourceFingerprint)
                return Result(RecoveryOutcome.SourceStateObserved, "当前文件匹配操作前状态；不证明业务连接正常。");
            if (current == entry.ExpectedFingerprint)
                return Result(RecoveryOutcome.ExpectedStateObserved, "当前文件匹配预期写后状态；仍需业务验证，未自动回滚。");
            return Result(RecoveryOutcome.Conflict, "当前文件与操作前后状态均不符，可能被外部修改或写入中断；禁止自动覆盖。");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            return Result(RecoveryOutcome.Unavailable, "无法安全读取当前文件状态；未执行任何恢复写入。");
        }

        RecoveryAssessment Result(RecoveryOutcome outcome, string explanation) =>
            new(entry.Id, entry.Plan.TargetId, entry.Stage, outcome, explanation);
    }
}
