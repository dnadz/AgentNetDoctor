using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.Adapters;

public abstract class FileAgentAdapter(string id, string form) : IAgentAdapter
{
    protected abstract IReadOnlyList<(string Scope, string Path)> GetConfigurationPaths();

    public Task<IReadOnlyList<ToolInstance>> DiscoverAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ToolInstance> instances = InstalledProductReader.Read(cancellationToken)
            .Where(item => ProductIdentity.Matches(id, item))
            .Select(item => new ToolInstance(id, form + "（卸载元数据候选）", item.Version, null)).Distinct().ToArray();
        return Task.FromResult(instances);
    }

    public async Task<AgentConfiguration> ReadConfigurationAsync(ToolInstance tool, CancellationToken cancellationToken)
    {
        if (tool.Id != id) throw new ArgumentException("目标与适配器不匹配", nameof(tool));
        List<ConfigurationSource> sources = [];
        foreach (var (scope, path) in GetConfigurationPaths())
            sources.Add(await ConfigurationFileReader.ReadAsync(id, scope, path, cancellationToken));
        return new(id, ObservationStatus.Partial, "只读候选来源（未确认完整覆盖链）", "EFFECTIVE_CONFIGURATION_NOT_VERIFIED", sources);
    }

    public AdapterCapabilities GetCapabilities(ToolInstance tool) =>
        new(true, [], "配置可读不代表版本/代理机制经过业务验收，当前禁止写入");
}
