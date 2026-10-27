namespace AgentNetDoctor.Core.Adapters;

public sealed record InstalledProduct(string DisplayName, string? Version, string Source);

public static class ProductIdentity
{
    public static bool Matches(string productId, InstalledProduct candidate)
    {
        var name = productId switch
        { "workbuddy" => "WorkBuddy", "antigravity" => "Antigravity", "codex" => "Codex", "claude-code" => "Claude Code", _ => null };
        return name is not null && (candidate.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(candidate.Version) && candidate.DisplayName.Equals(name + " " + candidate.Version, StringComparison.OrdinalIgnoreCase));
    }
}
