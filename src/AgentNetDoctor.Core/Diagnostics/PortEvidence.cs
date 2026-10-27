using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Diagnostics;

public static class PortEvidence
{
    public static bool MatchesProcess(Observation<ProcessIdentity> owner, ProcessIdentity current) =>
        owner.Status == ObservationStatus.Success && owner.Value is { } original &&
        original.Pid == current.Pid && original.StartedAt == current.StartedAt &&
        string.Equals(original.ExecutablePath, current.ExecutablePath, StringComparison.OrdinalIgnoreCase);
}
