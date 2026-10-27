namespace AgentNetDoctor.Core.Models;

public sealed record Finding(string Id, string RuleId, string? ToolId, Attribution Attribution,
    IReadOnlyList<string> EvidenceIds, string Explanation, string NextStep);
