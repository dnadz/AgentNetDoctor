using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Adapters;

public static class ToolConfigurationCandidates
{
    public static async Task<IReadOnlyList<ConfigurationSource>> ReadAsync(string toolId, CancellationToken cancellationToken)
    {
        var folder = toolId switch { "vscode" => "Code", "cursor" => "Cursor", "qoder" => "Qoder", "trae-cn" => "Trae CN", "trae-work-cn" => "TRAE Work CN", "antigravity" => "Antigravity", _ => null };
        if (folder is null) return [];
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, "User", "settings.json");
        return [await ConfigurationFileReader.ReadAsync("vscode", "候选用户设置（不证明当前版本/项目/托管覆盖）", path, cancellationToken)];
    }
}
