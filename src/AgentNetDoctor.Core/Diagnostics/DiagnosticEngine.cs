using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Core.Diagnostics;

public sealed class DiagnosticEngine
{
    public IReadOnlyList<Finding> Analyze(NetworkSnapshot snapshot,
        IReadOnlyList<AgentConfiguration> configurations, IReadOnlyList<ProbeResult> probes)
    {
        List<Finding> findings = [];
        if (snapshot.UserProxy.Status != ObservationStatus.Success)
            Add("PROXY_OBSERVATION_INCOMPLETE", Attribution.Unknown, ["user-proxy"],
                "当前用户代理信息不完整，不能判定网络正常或失效。", "先核实代理来源；不修改系统设置。");
        if (snapshot.UserProxy.Value is { Enabled: true, PacConfigured: true })
            Add("MULTIPLE_PROXY_SOURCES", Attribution.Probable, ["user-proxy"],
                "观察到手动代理及PAC配置，实际生效来源尚未确定。", "在目标应用中核实实际采用的代理。");
        foreach (var probe in probes.Where(p => p.SnapshotId == snapshot.Id))
        {
            if (probe.HttpStatus.Status == ObservationStatus.Failed)
                Add("PROBE_FAILED", Attribution.Unknown, [probe.EndpointId],
                    $"{probe.EndpointId} 的指定探测路径未成功；原因码：{probe.HttpStatus.ErrorCode}。",
                    "对比另一条路径；本次失败不能证明端口冲突或服务端不可用。");
            else if (probe.HttpStatus.Value >= 300)
                Add("HTTP_STATUS_REQUIRES_CONTEXT", Attribution.Unknown, [probe.EndpointId],
                    $"收到HTTP {probe.HttpStatus.Value}，说明有HTTP响应，尚不能证明账号或业务可用。",
                    "在目标Agent核实认证、权限和实际业务；不直接判定地区限制。");
        }
        return findings;

        void Add(string rule, Attribution attribution, string[] evidence, string explanation, string next) =>
            findings.Add(new($"finding-{findings.Count + 1}", rule, null, attribution, evidence, explanation, next));
    }
}
