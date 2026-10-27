using AgentNetDoctor.Core.Collection;
using AgentNetDoctor.Core.Models;
using System.ComponentModel;
using System.Net.NetworkInformation;

namespace AgentNetDoctor.Mac;

public sealed record MacInspection(NetworkSnapshot Snapshot, IReadOnlyList<Uri> LocalProxyEndpoints, IReadOnlyList<string> Notes);
public sealed class MacSnapshotCollector(IMacCommandRunner? runner = null) : ISnapshotCollector
{
    public async Task<MacInspection> InspectAsync(CancellationToken cancellationToken)
    {
        var commands = runner ?? new FixedCommandRunner();
        var observed = DateTimeOffset.UtcNow;
        List<string> notes = ["仅观察当前权限可见信息；进程名称/路径为候选，未验证应用签名或业务。快照非原子，不保证完整覆盖其他用户、共享宿主或瞬时连接。",
            "系统代理/PAC与应用内代理、Shell环境、VPN/TUN可能不同；本版未读取Agent私有配置，不能声称这些设置已生效。"];
        async Task<CommandResult?> Read(string command)
        {
            try
            {
                var result = await commands.RunAsync(command, cancellationToken);
                if (result.ExitCode == 0 || command == "ports" && result.ExitCode == 1 && result.Output.Length == 0) return result;
                notes.Add(command + "采集失败；不据此判断未安装或无代理。");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { notes.Add(command + "采集超时，结果未知。"); }
            catch (Exception error) when (error is IOException or Win32Exception or UnauthorizedAccessException or PlatformNotSupportedException)
            { notes.Add(command + "采集不可用：" + error.GetType().Name); }
            return null;
        }

        var proxyText = await Read("proxy");
        MacProxy? proxy = null;
        if (proxyText is not null)
        {
            try { proxy = MacParsers.ParseProxy(proxyText.Output); }
            catch (FormatException) { notes.Add("系统代理格式未识别，不能按关闭处理。"); }
        }
        var beforeText = await Read("processes");
        var portText = await Read("ports");
        var afterText = await Read("processes");
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyDictionary<int, ProcessIdentity>? stable = null;
        if (beforeText is not null && afterText is not null)
        {
            var before = MacParsers.ParseProcesses(beforeText.Output);
            var after = MacParsers.ParseProcesses(afterText.Output);
            stable = before.Where(pair => after.TryGetValue(pair.Key, out var current) && current == pair.Value).ToDictionary();
        }
        IReadOnlyList<ToolInstance>? tools = stable?.Values.Select(process => (Process: process, Tool: MacToolMatcher.Match(process.ExecutablePath)))
            .Where(item => item.Tool is not null).Select(item => new ToolInstance(item.Tool!.Id, "macOS运行候选", null, item.Process)).ToArray();
        IReadOnlyList<PortRecord>? ports = portText is null ? null : MacParsers.ParsePorts(portText.Output, stable ?? new Dictionary<int, ProcessIdentity>());
        Observation<IReadOnlyList<InterfaceRecord>> interfaces;
        try
        {
            var records = NetworkInterface.GetAllNetworkInterfaces().Select(item =>
            {
                var properties = item.GetIPProperties();
                return new InterfaceRecord(item.Name, item.NetworkInterfaceType.ToString(), item.OperationalStatus.ToString(),
                    properties.DnsAddresses.Select(ip => ip.ToString()).ToArray(), properties.GatewayAddresses.Select(gateway => gateway.Address.ToString()).ToArray());
            }).ToArray();
            interfaces = new(ObservationStatus.Partial, records, "CONFIGURATION_NOT_EFFECTIVE_ROUTE_PROOF", observed);
        }
        catch (Exception error) when (error is NetworkInformationException or PlatformNotSupportedException)
        { interfaces = new(ObservationStatus.Partial, null, "INTERFACES_UNAVAILABLE", observed); }
        var proxyStatus = proxy is null || proxyText!.Output.Contains("__SCOPED__", StringComparison.Ordinal) ? ObservationStatus.Partial : ObservationStatus.Success;
        if (proxyStatus == ObservationStatus.Partial) notes.Add("代理采集不完整或包含接口专属配置；全局结果不能代表VPN实际路由。");
        var snapshot = new NetworkSnapshot(Guid.NewGuid().ToString("N"), 1, observed,
            new(proxyStatus, proxy?.Settings, proxyStatus == ObservationStatus.Success ? null : "PROXY_PARTIAL", observed),
            new(ObservationStatus.Partial, ports, "VISIBLE_TCP_ONLY", observed),
            new(ObservationStatus.Partial, tools, "CANDIDATE_IDENTITIES_ONLY", observed), ObservationStatus.Partial,
            new(interfaces, new(ObservationStatus.NotApplicable, null, "WINDOWS_ONLY", observed), ObservationStatus.NotTested));
        return new(snapshot, proxy?.LoopbackEndpoints ?? [], notes);
    }
    public async Task<NetworkSnapshot> CollectAsync(CancellationToken cancellationToken) => (await InspectAsync(cancellationToken)).Snapshot;
}
