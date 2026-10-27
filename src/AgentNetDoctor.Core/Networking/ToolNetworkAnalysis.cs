using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;
using System.Net;

namespace AgentNetDoctor.Core.Networking;

public sealed record ToolNetworkAnalysis(string ToolId, int CandidateProcessCount, IReadOnlyList<PortRecord> Connections, IReadOnlyList<string> Actions);
public static class ToolNetworkAnalyzer
{
    public static ToolNetworkAnalysis Analyze(string id, NetworkSnapshot snapshot, IReadOnlyList<ProbeResult> probes)
    {
        var tool = ToolCatalog.All.SingleOrDefault(item => item.Id == id) ?? throw new ArgumentException("未知软件", nameof(id));
        var identities = (snapshot.Tools.Value ?? []).Where(item => item.Id == id && item.Process is not null).Select(item => item.Process!).ToHashSet();
        var ports = (snapshot.Ports.Value ?? []).Where(port => port.Owner.Value is { } owner && identities.Contains(owner)).ToArray();
        List<string> actions = [];
        if (identities.Count == 0) actions.Add("尚未取得可关联的运行进程，请正常打开该软件后重新检查；不把共享宿主误认成本软件。");
        if (snapshot.UserProxy.Value is { PacConfigured: true } or { AutoDetect: true }) actions.Add("存在PAC/自动发现，需确认应用采用的实际路径；不要直接清空系统代理。");
        if (ports.Any(port => port.State == "SYN_SENT")) actions.Add("观察到连接尝试中的TCP记录，可能正在等待；对照目标DNS/TCP和代理路径复测，不能单凭状态判定被封锁。");
        if (ports.Any(port => port.State == "ESTABLISHED")) actions.Add("观察到已建立TCP连接；若应用仍失败，继续检查TLS、认证与流式响应，不能据此声称业务正常。");
        var current = probes.Where(probe => probe.SnapshotId == snapshot.Id && probe.EndpointId.StartsWith(id + ":", StringComparison.Ordinal)).ToArray();
        var direct = current.LastOrDefault(probe => probe.Path == ProbePath.NoExplicitProxy);
        var proxy = current.LastOrDefault(probe => probe.Path == ProbePath.ExplicitProxy && probe.EndpointId == direct?.EndpointId);
        var local = direct is not null && Uri.TryCreate("http://" + direct.EndpointId[(id.Length + 1)..], UriKind.Absolute, out var endpoint) &&
            (endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));
        if (local) actions.Add("本机服务不应通过远端HTTP代理作路径对比；先检查本机监听地址与端口，以及该客户端的本地绕过配置。");
        else if (proxy?.HttpStatus.Value == 407) actions.Add("代理返回407，需要代理认证；未证明目标服务可达，不推荐据此切换代理或把密码写入报告。");
        else if (direct?.HttpStatus.Status == ObservationStatus.Failed && proxy?.HttpStatus.Status == ObservationStatus.Success && proxy.HttpStatus.Value is >= 200 and < 300)
            actions.Add("本次目标经指定代理收到HTTP响应、无显式代理失败：优先在本软件内部选择已测试的代理，避免覆盖直连工具的系统设置。仍需应用业务验证。");
        else if (direct?.HttpStatus.Status == ObservationStatus.Success && direct.HttpStatus.Value is >= 200 and < 300 && proxy?.HttpStatus.Status == ObservationStatus.Failed)
            actions.Add("本次无显式代理收到HTTP响应、指定代理失败：优先检查本软件代理来源与端口，测试应用内直连选项；无显式代理仍可能经过TUN。");
        else if (direct?.HttpStatus.Status == ObservationStatus.Failed && proxy?.HttpStatus.Status == ObservationStatus.Failed)
            actions.Add("两条已测路径均失败：按DNS→端口→TLS/HTTP错误分层检查，暂不修改全局代理、DNS或VPN规则。");
        actions.Add(id switch
        {
            "ollama" => "本地推理与模型下载分别优化：先确认OLLAMA_HOST指向的本机服务；本机请求避免被远端代理接管，模型下载单独检查HTTPS代理。不把服务绑定到0.0.0.0作为通用修复。",
            "anythingllm" or "chatbox" => "先确定实际模型提供商和嵌入服务；本地Ollama与外部API分开测试，不因其中一条失败更换全部代理。",
            "github-desktop" => "GitHub API、Git HTTPS和SSH是不同路径；API成功不证明clone/push成功。先核对Git专属代理，不修改凭据或仓库远端。",
            "github-copilot" => "区分桌面、VS Code插件和终端入口，分别核实宿主及扩展代理；GitHub网站成功不证明Copilot认证和模型流可用。",
            "google-cloud-sdk" => "核对gcloud专属代理配置与当前Shell环境；认证、下载和具体Google Cloud API分别测试，不清空整个用户环境。",
            "vscode" or "cursor" or "qoder" or "trae-cn" or "trae-work-cn" or "antigravity" => "核对应用设置、扩展和集成终端三层代理，保留证书校验；本窗口读取候选用户配置，项目/托管/远程覆盖仍须核实。",
            "obsidian" => "先区分本地笔记、同步、社区插件网络；只排查出错的网络功能，不扫描或改写笔记库。",
            "workbuddy" => "在WorkBuddy设置→系统设置→网络中选择直连/跟随系统/已验证代理；正常关闭VPN后再发起新请求。不要套用CodeBuddy磁盘配置。",
            "codex" => "区分桌面/CLI/IDE和认证/模型入口，核对启动时代理环境与提供商；不要把sandbox网络代理当作客户端上游代理。",
            "deepseek-harness" => "核对Harness实际模型提供商与服务地址；DeepSeek API和应用登录/更新是不同路径，不读取或导出API Key。",
            "claude-desktop" => "按Claude桌面自身网络与认证链排查；Claude Code的env配置不代表桌面应用已生效。",
            "chatgpt" => "核实桌面应用登录与对话链路；API入口成功不等于ChatGPT桌面对话成功。",
            "qoderwake" or "grok-bot" => "交互界面与后台Agent任务分别核对，后台进程可能保留旧代理环境；先保存任务并正常重启，不强杀。",
            "doubao" => "区分桌面登录、对话和附件上传；先对实际失败的服务测试，避免仅依据官网或单次HTTP响应改代理。",
            _ => tool.Note
        });
        return new(id, identities.Count, ports, actions);
    }
}
