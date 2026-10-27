namespace AgentNetDoctor.Core.Repair;

public enum MutationDirection { Apply, Rollback }
public enum JournalStage { Prepared, Completed }
public sealed record RepairJournalEntry(string Id, RepairPlan Plan, string BackupId,
    MutationDirection Direction, string SourceFingerprint, string ExpectedFingerprint, JournalStage Stage);

public interface IRepairJournal
{
    Task SaveAsync(RepairJournalEntry entry, CancellationToken cancellationToken);
    Task<RepairJournalEntry> ReadAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
}
