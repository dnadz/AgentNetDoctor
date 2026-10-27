namespace AgentNetDoctor.Adapters.Antigravity;

public sealed class AntigravityAdapter : FileAgentAdapter
{
    public AntigravityAdapter() : base("antigravity", "IDE/桌面形态待核实") { }
    protected override IReadOnlyList<(string Scope, string Path)> GetConfigurationPaths() =>
        [("旧版IDE候选（不代表新桌面或语言服务）", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Antigravity", "User", "settings.json"))];
}
