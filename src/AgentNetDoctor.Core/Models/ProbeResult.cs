namespace AgentNetDoctor.Core.Models;

public enum ProbePath { NoExplicitProxy, ExplicitProxy, ProcessDefault }
public sealed record ProbeResult(string EndpointId, ProbePath Path, string SnapshotId,
    Observation<int?> HttpStatus, TimeSpan Elapsed);
