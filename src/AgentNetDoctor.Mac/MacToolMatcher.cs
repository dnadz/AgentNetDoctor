using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Mac;

public static class MacToolMatcher
{
    public static CatalogTool? Match(string path)
    {
        var app = path.IndexOf(".app/Contents/", StringComparison.OrdinalIgnoreCase);
        if (app >= 0)
        {
            var name = Path.GetFileName(path[..app]);
            return ToolCatalog.All.FirstOrDefault(tool => tool.InstalledNames.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
        var executable = Path.GetFileName(path);
        // Shared node/Electron hosts and Claude CLI must not impersonate desktop apps.
        return executable.ToLowerInvariant() switch
        {
            "codex" => ToolCatalog.All.Single(tool => tool.Id == "codex"),
            "ollama" => ToolCatalog.All.Single(tool => tool.Id == "ollama"),
            "gcloud" => ToolCatalog.All.Single(tool => tool.Id == "google-cloud-sdk"),
            _ => null
        };
    }
}
