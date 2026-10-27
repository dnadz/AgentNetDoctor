using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Adapters;

public sealed record AdapterCapabilities(bool CanRead, IReadOnlyList<string> WritableFields, string Reason);
public sealed record ConfigurationSource(string Scope, ObservationStatus Status, string? ErrorCode, ConfigurationInspection? Inspection);
public sealed record AgentConfiguration(string ToolId, ObservationStatus Status, string? ProxySource, string? ErrorCode,
    IReadOnlyList<ConfigurationSource>? Sources = null);

public interface IAgentAdapter
{
    Task<IReadOnlyList<ToolInstance>> DiscoverAsync(CancellationToken cancellationToken);
    Task<AgentConfiguration> ReadConfigurationAsync(ToolInstance tool, CancellationToken cancellationToken);
    AdapterCapabilities GetCapabilities(ToolInstance tool);
}

public static class CapabilityPolicy
{
    public static AdapterCapabilities ForVersion(string? version, IReadOnlySet<string> verifiedVersions) =>
        new(true, [], "尚无经实测核实的可写字段；仅可读检查");
}
