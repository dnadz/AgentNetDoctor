using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Adapters;

public static class RuntimeRequirements
{
    public static Observation<string> CheckClaudeNode(string? cliVersion, string form, Observation<string> node)
    {
        if (form == "native") return Result(ObservationStatus.NotApplicable, "原生形态不要求外部Node", null);
        // Only this installed package manifest was inspected; do not generalize its engines constraint.
        if (form != "npm" || cliVersion != "2.1.296") return Result(ObservationStatus.NotTested, null, "RUNTIME_REQUIREMENT_NOT_VERIFIED");
        if (node.Status != ObservationStatus.Success) return Result(node.Status, null, node.ErrorCode ?? "NODE_OBSERVATION_INCOMPLETE");
        var raw = node.Value?.Trim();
        if (raw?.StartsWith('v') == true) raw = raw[1..];
        if (!Version.TryParse(raw, out var version)) return Result(ObservationStatus.NotTested, null, "NODE_VERSION_UNRECOGNIZED");
        return version.Major >= 22 ? Result(ObservationStatus.Success, "满足已观察npm包的Node>=22要求", null)
            : Result(ObservationStatus.Failed, null, "NODE_VERSION_TOO_OLD");

        static Observation<string> Result(ObservationStatus status, string? value, string? error) => new(status, value, error, DateTimeOffset.UtcNow);
    }
}
