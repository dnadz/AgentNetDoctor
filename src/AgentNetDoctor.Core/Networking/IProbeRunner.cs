using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Networking;

public sealed record EndpointSpec(string Id, Uri Uri, string Purpose);
public sealed record ProbeRequest(EndpointSpec Endpoint, ProbePath Path, Uri? ProxyUri, string SnapshotId);

public interface IProbeRunner
{
    Task<ProbeResult> RunAsync(ProbeRequest request, CancellationToken cancellationToken);
}
