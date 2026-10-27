namespace AgentNetDoctor.Core.Models;

public sealed record ProcessIdentity(int Pid, DateTimeOffset StartedAt, string ExecutablePath);
public sealed record ToolInstance(string Id, string Form, string? Version, ProcessIdentity? Process);
public sealed record ProxySettings(bool Enabled, string? Server, bool PacConfigured, bool AutoDetect);
public sealed record PortRecord(string Address, int Port, string State, Observation<ProcessIdentity> Owner, string? RemoteAddress = null, int? RemotePort = null);
public sealed record NetworkSnapshot(
    string Id, int SchemaVersion, DateTimeOffset ObservedAt,
    Observation<ProxySettings> UserProxy,
    Observation<IReadOnlyList<PortRecord>> Ports,
    Observation<IReadOnlyList<ToolInstance>> Tools,
    ObservationStatus Status, NetworkDetails? Network = null);
