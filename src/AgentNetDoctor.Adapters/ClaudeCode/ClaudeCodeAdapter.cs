namespace AgentNetDoctor.Adapters.ClaudeCode;

public sealed class ClaudeCodeAdapter : FileAgentAdapter
{
    public ClaudeCodeAdapter() : base("claude-code", "本机CLI候选") { }
    protected override IReadOnlyList<(string Scope, string Path)> GetConfigurationPaths()
    {
        var home = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        return Path.IsPathFullyQualified(home) ? [("用户配置（不含项目/托管/进程覆盖）", Path.Combine(home, "settings.json"))] : [];
    }
}
