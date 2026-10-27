using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Collection;

public interface ISnapshotCollector
{
    Task<NetworkSnapshot> CollectAsync(CancellationToken cancellationToken);
}
