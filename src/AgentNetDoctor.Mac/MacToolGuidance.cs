namespace AgentNetDoctor.Mac;

public static class MacToolGuidance
{
    public static string For(string id) => id switch
    {
        "ollama" => "本地推理与模型下载分开检查；先核实实际OLLAMA_HOST和本机监听，避免把本机请求送到远端代理。不要为排障开放0.0.0.0。",
        "anythingllm" or "chatbox" => "确认实际模型提供商、嵌入服务与本地Ollama地址，分别测试；不同功能不一定使用同一代理。",
        "codex" => "区分Codex桌面、CLI与IDE入口。Mac图形应用和Shell继承环境可能不同；认证与模型服务分别核实，不套用Windows配置路径。",
        "claude-desktop" => "这是Claude桌面候选，不能据此证明Claude Code存在或代理已生效；桌面登录与对话链路分别核实。",
        "chatgpt" => "ChatGPT桌面与OpenAI API是不同服务；API根地址有响应不证明桌面对话可用。",
        "github-desktop" => "Git HTTPS、SSH和GitHub API分开测试；应用连接不能证明Git子进程代理生效，不修改凭据或远端。",
        "github-copilot" => "按实际安装形态核实宿主/插件，不把所有Electron或node进程当作Copilot；GitHub网站响应不代表补全/Agent正常。",
        "google-cloud-sdk" => "核实实际gcloud命令及该Shell环境；认证、更新下载与目标Cloud API分开验证。不把终端窗口当作SDK证据。",
        "vscode" or "cursor" or "qoder" or "antigravity" => "按Mac应用实际支持的设置检查应用、扩展、集成终端三层代理。本版未读取用户/工作区配置，不宣称生效；保留证书校验。",
        "obsidian" => "区分本地笔记、同步和社区插件；只测试出错的网络功能，不扫描笔记库。",
        "deepseek-harness" => "先核实该Mac发行来源和实际提供商。其他软件调用DeepSeek API不代表Harness正在运行；登录、模型服务分别测试。",
        "workbuddy" or "doubao" or "qoderwake" or "grok-bot" or "trae-cn" or "trae-work-cn" => "该软件Mac发行/具体版本与网络设置入口尚未核实。若已安装，请以真实应用中提供的设置为准；后台任务可能保留旧环境，不套用其他产品或Windows配置。",
        _ => "当前Mac适配尚未核实，不执行配置修改。"
    };
}
