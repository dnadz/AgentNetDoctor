namespace AgentNetDoctor.Core.Models;

public sealed record InterfaceRecord(string Name, string Type, string OperationalState,
    IReadOnlyList<string> DnsServers, IReadOnlyList<string> Gateways);
public sealed record WinHttpDefaults(uint AccessType, bool HasProxy, bool HasBypass);
public sealed record NetworkDetails(Observation<IReadOnlyList<InterfaceRecord>> Interfaces,
    Observation<WinHttpDefaults> WinHttp, ObservationStatus EffectiveRoutesStatus);
