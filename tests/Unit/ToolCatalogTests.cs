using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.UnitTests;

public sealed class ToolCatalogTests
{
    [Fact]
    public void ScopeIsExactlyScreenshotSoftwarePlusCodex()
    {
        string[] expected = ["antigravity", "anythingllm", "chatbox", "cursor", "deepseek-harness", "github-copilot", "github-desktop",
            "google-cloud-sdk", "grok-bot", "obsidian", "ollama", "qoder", "qoderwake", "trae-cn", "trae-work-cn", "vscode", "workbuddy", "doubao", "chatgpt", "claude-desktop", "codex"];
        Assert.Equal(expected.Order(), ToolCatalog.All.Select(tool => tool.Id).Order());
        Assert.Equal(21, ToolCatalog.All.Select(tool => tool.Id).Distinct().Count());
    }
    [Fact]
    public void ExactCandidateMatchingDoesNotConfuseRelatedToolsOrSharedHosts()
    {
        Assert.Equal("qoderwake", ToolCatalog.MatchProcess("QoderWake")?.Id);
        Assert.Equal("qoder", ToolCatalog.MatchProcess("qoder")?.Id);
        Assert.Equal("trae-work-cn", ToolCatalog.MatchInstalled("TRAE Work CN", "1")?.Id);
        Assert.Equal("trae-cn", ToolCatalog.MatchInstalled("Trae CN", "1")?.Id);
        Assert.Equal("github-desktop", ToolCatalog.MatchInstalled("GitHub Desktop", "3")?.Id);
        Assert.Equal("anythingllm", ToolCatalog.MatchInstalled("AnythingLLM 1.2.3", "1.2.3")?.Id);
        Assert.Null(ToolCatalog.MatchInstalled("Claude Code", "1"));
        Assert.Null(ToolCatalog.MatchInstalled("QoderWake Fake", "1"));
        Assert.Null(ToolCatalog.MatchProcess("cmd"));
        Assert.Null(ToolCatalog.MatchProcess("node"));
        Assert.Null(ToolCatalog.MatchProcess("copilot"));
        Assert.Null(ToolCatalog.MatchProcess("CursorUpdater"));
    }
    [Fact]
    public void InventoryShowsAllRequestedSoftwareWithoutClaimingInstallationOrRepairSupport()
    {
        Assert.All(ToolInventory.Build([], [], false, false), row => Assert.Equal("未检查", row.Detection));
        var rows = ToolInventory.Build([new("qoderwake", "candidate", null, null)], [new("Qoder", "2.0", "fixture")], true, true);
        Assert.Equal(21, rows.Count);
        Assert.Contains("运行进程候选", rows.Single(row => row.Name == "QoderWake").Detection);
        Assert.Equal("2.0", rows.Single(row => row.Name == "Qoder").Versions);
        Assert.Contains("不代表未安装", rows.Single(row => row.Name == "Ollama").Detection);
        Assert.All(ToolInventory.Build([], [], true, false), row => Assert.Equal("安装元数据未完整取得", row.Detection));
    }
}
