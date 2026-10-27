namespace AgentNetDoctor.Adapters.WorkBuddy;

public sealed class WorkBuddyAdapter : FileAgentAdapter
{
    public WorkBuddyAdapter() : base("workbuddy", "独立桌面") { }
    // Native modes documented; disk schema for this version is not verified. Never read CodeBuddy as WorkBuddy.
    protected override IReadOnlyList<(string Scope, string Path)> GetConfigurationPaths() => [];
}
