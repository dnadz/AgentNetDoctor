namespace AgentNetDoctor.Core.Adapters;

public sealed record CatalogTool(string Id, string Name, string Category, IReadOnlyList<string> ProcessNames, IReadOnlyList<string> InstalledNames, string Note);

public static class ToolCatalog
{
    public static IReadOnlyList<CatalogTool> All { get; } = Array.AsReadOnly(new CatalogTool[]
    {
        new("antigravity", "Antigravity", "开发 Agent", ["antigravity"], ["Antigravity"], "IDE/桌面形态待核实；现有候选配置只读，禁止写入。"),
        new("anythingllm", "AnythingLLM", "本地/云端 AI 工作台", ["anythingllm"], ["AnythingLLM"], "网络取决于模型提供商、嵌入模型与本地服务；未读取配置。"),
        new("chatbox", "Chatbox", "AI 聊天客户端", ["chatbox"], ["Chatbox", "Chatbox AI"], "网络取决于所选提供商；不读取密钥。"),
        new("cursor", "Cursor", "AI 开发环境", ["cursor"], ["Cursor", "Cursor (User)"], "未核实应用代理、扩展与终端覆盖链。"),
        new("deepseek-harness", "DeepSeek Harness", "Agent 工作台", ["deepseek harness", "deepseek-harness", "dsh"], ["DeepSeek Harness"], "独立产品候选，不等同于其他工具内调用DeepSeek API。"),
        new("github-copilot", "GitHub Copilot", "编程 Agent / 插件", [], ["GitHub Copilot"], "桌面/插件形态待核实；不把共享Code/node/cmd进程作为Copilot证据。"),
        new("github-desktop", "GitHub Desktop", "Git 开发配套", ["githubdesktop"], ["GitHub Desktop"], "Git客户端，不是独立编程Agent；Git与应用连接路径可能不同。"),
        new("google-cloud-sdk", "Google Cloud SDK Shell", "云开发命令行", [], ["Google Cloud SDK", "Google Cloud CLI"], "SDK Shell是命令行入口，不把所有cmd进程当作本产品。"),
        new("grok-bot", "Grok Bot", "通用 Agent", ["grok bot", "grok-bot"], ["Grok Bot"], "按截图名称加入；具体发行来源、版本与网络机制未核实。"),
        new("obsidian", "Obsidian", "知识管理配套", ["obsidian"], ["Obsidian"], "本地笔记、同步与插件网络分开；未扫描笔记或插件配置。"),
        new("ollama", "Ollama", "本地模型运行服务", ["ollama", "ollama app"], ["Ollama"], "本地推理、下载模型与云端服务分开；不默认探测或修改11434端口。"),
        new("qoder", "Qoder", "开发 Agent", ["qoder"], ["Qoder", "Qoder (User)"], "与QoderWake独立识别，尚未验证代理机制。"),
        new("qoderwake", "QoderWake", "通用 Agent", ["qoderwake"], ["QoderWake"], "与Qoder独立识别，尚未验证后台任务与代理机制。"),
        new("trae-cn", "TRAE CN", "AI 开发环境", ["trae cn"], ["Trae CN", "TRAE CN (User)"], "中国版独立记录；不与TRAE Work CN或国际版合并。"),
        new("trae-work-cn", "TRAE Work CN", "Agent 工作台", ["trae work cn"], ["TRAE Work CN", "TRAE Work CN (User)"], "按截图独立加入，网络机制未验证。"),
        new("vscode", "Visual Studio Code", "编辑器 / Agent 宿主", ["code"], ["Microsoft Visual Studio Code", "Microsoft Visual Studio Code (User)", "Visual Studio Code"], "宿主不代表其插件和集成终端使用同一代理。"),
        new("workbuddy", "WorkBuddy", "桌面 Agent", ["workbuddy"], ["WorkBuddy"], "不套用CodeBuddy配置；当前只读元数据，应用内网络模式需核实。"),
        new("doubao", "豆包", "AI 桌面助手", ["doubao", "豆包"], ["豆包", "Doubao"], "只观察产品候选，不读取聊天记录或账号。"),
        new("chatgpt", "ChatGPT", "AI 桌面助手", ["chatgpt"], ["ChatGPT"], "与Codex独立记录；未核实商店安装或实际业务路径。"),
        new("claude-desktop", "Claude", "AI 桌面助手", ["claude"], ["Claude"], "截图是桌面版，不等同Claude Code；同名进程不能确认形态。"),
        new("codex", "Codex", "开发 Agent", ["codex"], ["Codex"], "区分桌面/CLI/IDE；候选配置只读，真实启动与写入未解锁。")
    }.Select(tool => tool with { ProcessNames = Array.AsReadOnly(tool.ProcessNames.ToArray()), InstalledNames = Array.AsReadOnly(tool.InstalledNames.ToArray()) }).ToArray());

    public static CatalogTool? MatchProcess(string name) => All.FirstOrDefault(tool => tool.ProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase));
    public static CatalogTool? MatchInstalled(string name, string? version) => All.FirstOrDefault(tool => tool.InstalledNames.Any(alias =>
        name.Equals(alias, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(version) && name.Equals(alias + " " + version, StringComparison.OrdinalIgnoreCase)));
}
