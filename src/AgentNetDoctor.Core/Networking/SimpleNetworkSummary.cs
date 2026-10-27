using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Networking;

public sealed record SimpleNetworkSummary(string Title, string Detail, string NextStep)
{
    public static SimpleNetworkSummary Create(string name, ToolNetworkAnalysis analysis, bool failedLocalProxy, IReadOnlyList<ProbeResult> probes)
    {
        if (probes.Any(probe => probe.HttpStatus.Value == 407))
            return new("代理程序需要登录或授权", "这次连接被代理程序要求验证，还不能确认软件服务器是否正常。", "请先在你使用的代理程序中处理登录或授权，再重新测试。不要在这里粘贴密码。");
        if (failedLocalProxy)
            return new("发现一项需要核对的代理设置", "电脑中记录的代理入口没有接受连接。代理程序可能已经关闭，但仍需确认这个软件是否采用它。", "如果刚退出代理或VPN，可先正常打开它再重试。需要直连时，优先检查该软件自己的网络设置，不要重置全部网络。");
        if (probes.Any(probe => probe.HttpStatus.Status == ObservationStatus.Failed))
            return new("服务连接还有问题", "至少一条测试路线没有完成连接。这不能单独证明软件服务器坏了，也不能据此确定VPN或账号有问题。", "先确认网络和代理程序是否正常，再回到软件重试。需要他人协助时，可在高级选项保存报告。");
        if (analysis.CandidateProcessCount == 0)
            return new("暂时无法确认软件的运行连接", $"没有取得能够关联到{name}的运行信息。它可能尚未打开，或采用了本工具还不能识别的启动方式；不表示没有安装。", "请先正常打开这个软件，再点“重新检查”。仍没有结果时，可保存报告请人协助。");
        if (probes.Count > 0)
            return new("已收到测试地址的回应", "填写的服务地址有回应，但还不能确认这个软件已采用相同连接方式，也不能保证登录或发送消息成功。", $"请回到{name}重试原来的操作。如果仍失败，保存报告后再检查软件内的账号、提供商或网络设置。");
        return new("电脑端检查已完成", "已检查能够取得的软件连接和代理信息。尚未测试软件服务器，所以不能把这次结果当作全部网络正常。", $"请先回到{name}重试。仍连不上时，可展开高级选项继续测试服务，或保存报告请熟悉电脑的人协助。");
    }
}
