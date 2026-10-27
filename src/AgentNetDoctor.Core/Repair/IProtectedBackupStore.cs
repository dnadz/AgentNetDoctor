namespace AgentNetDoctor.Core.Repair;

public interface IProtectedBackupStore
{
    Task<string> SaveAsync(string targetId, byte[] original, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string backupId, string targetId, CancellationToken cancellationToken);
}
