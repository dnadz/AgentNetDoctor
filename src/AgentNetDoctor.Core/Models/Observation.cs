namespace AgentNetDoctor.Core.Models;

public enum ObservationStatus
{
    Success, Failed, NotTested, NotApplicable, PermissionDenied, UnsupportedVersion, Partial
}

public enum Attribution { Confirmed, Probable, Unknown }

public sealed record Observation<T>(ObservationStatus Status, T? Value, string? ErrorCode, DateTimeOffset ObservedAt);
public sealed record Evidence(string Id, ObservationStatus Status, string? Value, string? ErrorCode);
public sealed record ReportData(string Id, IReadOnlyList<Evidence> Evidence);
