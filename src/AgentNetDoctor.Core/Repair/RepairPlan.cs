using System.Security.Cryptography;
using System.Text.Json;
using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Core.Repair;

public enum ActionKind { CreateLaunchProfile, PatchAppFields, RestoreUserManualProxy, LaunchApprovedTarget }
public sealed record FieldChange(string Field, string? Before, string? After);
public sealed record RepairPlan(string Id, ActionKind Kind, string ToolId, string TargetId, string TargetVersion,
    string BeforeFingerprint, IReadOnlyList<FieldChange> Changes, string Scope, string Verification, string Rollback);
public sealed record ConfirmedPlan(string PlanId, string Digest);

public static class RepairPlanConfirmation
{
    public static string Digest(RepairPlan plan) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));
    public static ConfirmedPlan FromReviewedPlan(RepairPlan plan) => new(plan.Id, Digest(plan));
    public static bool Matches(RepairPlan plan, ConfirmedPlan confirmation) =>
        plan.Id == confirmation.PlanId && Digest(plan) == confirmation.Digest;
}

public static class RepairPlanner
{
    public static RepairPlan? PlanAppFields(string toolId, string? version, AdapterCapabilities capabilities,
        string targetId, string beforeFingerprint, IReadOnlyList<FieldChange> changes, bool policyUnknown)
    {
        if (string.IsNullOrWhiteSpace(version) || policyUnknown || !capabilities.CanRead || changes.Count == 0 ||
            targetId.Length is < 1 or > 64 || targetId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return null;
        if (changes.Select(change => change.Field).Distinct(StringComparer.Ordinal).Count() != changes.Count) return null;
        foreach (var change in changes)
            if (change.Field != "http.proxy" || !capabilities.WritableFields.Contains(change.Field, StringComparer.Ordinal) ||
                !SafeProxy(change.Before) || !SafeProxy(change.After)) return null;
        return new(Guid.NewGuid().ToString("N"), ActionKind.PatchAppFields, toolId, targetId, version, beforeFingerprint,
            changes.ToArray(), "仅所选应用的已核实字段；不改提供商/账号/全局环境", "目标应用业务与跨目标回归均须验证", "仅在目标仍匹配本次写入时恢复受保护备份");
    }

    internal static bool SafeProxy(string? value) => string.IsNullOrEmpty(value) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "http" && uri.Host.Length > 0 &&
        uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/";
}
