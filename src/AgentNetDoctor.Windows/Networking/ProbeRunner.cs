using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
namespace AgentNetDoctor.Windows.Networking;

public sealed class ProbeRunner : IProbeRunner
{
    private readonly HttpProbeRunner shared = new();
    public Task<ProbeResult> RunAsync(ProbeRequest request, CancellationToken cancellationToken) => shared.RunAsync(request, cancellationToken);
}