namespace AgentNetDoctor.Adapters.Codex;

public sealed class CodexAdapter : FileAgentAdapter
{
    public CodexAdapter() : base("codex", "形态未确认") { }
    protected override IReadOnlyList<(string Scope, string Path)> GetConfigurationPaths()
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        // Do not guess relative CODEX_HOME or project trust/merging behavior.
        return Path.IsPathFullyQualified(home) ? [("用户配置（不含项目/托管覆盖）", Path.Combine(home, "config.toml"))] : [];
    }
}
