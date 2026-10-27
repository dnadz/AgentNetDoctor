using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Reporting;

namespace AgentNetDoctor.Core.Adapters;

public sealed record ToolInventoryRow(string Name, string Category, string Detection, string Versions, string Capability, string Note);
public static class ToolInventory
{
    public static IReadOnlyList<ToolInventoryRow> Build(IReadOnlyList<ToolInstance> running, IReadOnlyList<InstalledProduct> installed, bool inspected, bool metadataComplete)
    {
        return ToolCatalog.All.Select(tool =>
        {
            var processCount = running.Count(item => item.Id == tool.Id);
            var candidates = installed.Where(item => ToolCatalog.MatchInstalled(item.DisplayName, item.Version)?.Id == tool.Id).ToArray();
            var evidence = new List<string>();
            if (processCount > 0) evidence.Add($"运行进程候选 {processCount}");
            if (candidates.Length > 0) evidence.Add("安装元数据候选");
            var status = !inspected ? "未检查" : evidence.Count > 0 ? string.Join("；", evidence) :
                metadataComplete ? "未观察到（不代表未安装）" : "安装元数据未完整取得";
            var versions = candidates.Select(item => item.Version).OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
            return new ToolInventoryRow(tool.Name, tool.Category, status,
                versions.Length == 0 ? "未核实" : Redactor.Redact(string.Join(" / ", versions)),
                tool.Id is "codex" or "antigravity" ? "独立网络检测 / 候选配置只读" : "独立网络检测 / 配置待适配", tool.Note);
        }).ToArray();
    }
}
