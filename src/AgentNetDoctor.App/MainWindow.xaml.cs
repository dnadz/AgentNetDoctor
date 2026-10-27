using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using AgentNetDoctor.Core.Diagnostics;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Reporting;
using AgentNetDoctor.Core.Workflow;
using AgentNetDoctor.Windows.Collection;
using Microsoft.Win32;
using System.Security;
using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Adapters.Codex;
using AgentNetDoctor.Adapters.WorkBuddy;
using AgentNetDoctor.Adapters.Antigravity;
using AgentNetDoctor.Adapters;

namespace AgentNetDoctor.App;

public partial class MainWindow : Window
{
    private readonly CheckSession session = new();
    private readonly List<AgentConfiguration> configurations = [];
    private readonly List<ToolInstance> installedCandidates = [];
    private IReadOnlyList<InstalledProduct> catalogInstalled = [];
    private bool catalogMetadataComplete;
    private NetworkSnapshot? snapshot;
    private CancellationTokenSource? operation;

    public MainWindow()
    {
        InitializeComponent();
        HomeToolBox.ItemsSource = ToolCatalog.All;
        HomeToolBox.SelectedValue = "codex";
        CatalogGrid.ItemsSource = ToolInventory.Build([], [], false, false);
    }

    private async void Collect_Click(object sender, RoutedEventArgs e)
    {
        BeginOperation();
        session.StartCollection();
        snapshot = null;
        configurations.Clear();
        installedCandidates.Clear();
        catalogInstalled = [];
        catalogMetadataComplete = false;
        CatalogGrid.ItemsSource = ToolInventory.Build([], [], false, false);
        StatusText.Text = "正在只读检查…";
        try
        {
            var token = operation!.Token;
            snapshot = await Task.Run(async () =>
            {
                var collected = await new WindowsSnapshotCollector().CollectAsync(token);
                try { catalogInstalled = InstalledProductReader.Read(token); catalogMetadataComplete = true; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException) { catalogMetadataComplete = false; }
                IAgentAdapter[] adapters = [new CodexAdapter(), new WorkBuddyAdapter(), new AntigravityAdapter()];
                string[] ids = ["codex", "workbuddy", "antigravity"];
                for (int i = 0; i < adapters.Length; i++)
                {
                    try { installedCandidates.AddRange(await adapters[i].DiscoverAsync(token)); }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
                    { configurations.Add(new(ids[i], ex is IOException ? ObservationStatus.Failed : ObservationStatus.PermissionDenied, "安装元数据", "DISCOVERY_UNAVAILABLE")); }
                    configurations.Add(await adapters[i].ReadConfigurationAsync(new(ids[i], "未确认", null, null), token));
                }
                return collected;
            });
            session.FinishCollection();
            Render();
            StatusText.Text = "本机检查完成 · 部分信息尚未验证";
        }
        catch (OperationCanceledException) { StatusText.Text = "检查已取消"; session.Fail(); }
        catch (Exception ex) { ShowFailure(ex); }
        finally { EndOperation(); }
    }

    private void Render()
    {
        if (snapshot is null) return;
        var text = new StringBuilder();
        text.AppendLine("Agent 网络医生 0.1 · 本机检查报告");
        text.AppendLine($"采集时间：{snapshot.ObservedAt.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm:ss zzz}（北京时间）　整体状态：部分验证");
        text.AppendLine("\n【本机代理】");
        text.AppendLine($"读取状态：{Label(snapshot.UserProxy.Status)}");
        if (snapshot.UserProxy.Value is { } proxy)
            text.AppendLine($"当前用户手动代理：{(proxy.Enabled ? "开启" : "关闭")}；PAC 配置：{(proxy.PacConfigured ? "存在" : "未观察到")}");
        if (snapshot.UserProxy.Value is { } userProxy)
            text.AppendLine($"自动发现配置：{(userProxy.AutoDetect ? "开启" : "关闭")}（不主动请求PAC/WPAD）。");
        if (snapshot.Network is { } network)
        {
            text.AppendLine($"WinHTTP默认配置读取：{Label(network.WinHttp.Status)}；它可被每个会话覆盖，不代表Agent实际路径。");
            if (network.WinHttp.Value is { } defaults)
                text.AppendLine($"WinHTTP命名代理记录：{(defaults.HasProxy ? "存在" : "未观察到")}；不由此判定与用户代理冲突。");
            text.AppendLine("\n【网卡与已配置DNS】");
            text.AppendLine($"读取状态：{Label(network.Interfaces.Status)}");
            foreach (var adapter in network.Interfaces.Value ?? [])
                text.AppendLine($"{adapter.Name}　{adapter.Type}　{adapter.OperationalState}　DNS数：{adapter.DnsServers.Count}　网关数：{adapter.Gateways.Count}");
        }
        text.AppendLine("DNS可达性、有效路由/PAC解析及TUN退出后的恢复能力：尚未验证。虚拟网卡存在不代表故障。");
        text.AppendLine("\n【TCP 端口】");
        text.AppendLine($"读取状态：{Label(snapshot.Ports.Status)}；记录数：{snapshot.Ports.Value?.Count.ToString() ?? "未知"}");
        foreach (var port in snapshot.Ports.Value?.Where(p => p.State == "LISTENING").Take(100) ?? [])
            text.AppendLine($"{port.Address}:{port.Port}　监听　进程身份：{Label(port.Owner.Status)}");
        text.AppendLine("仅列出前100项监听记录；连接远端443不构成本地端口冲突证据。端口与进程非原子采集，不据此确认占用归因。");
        text.AppendLine("\n【Agent 运行候选】");
        foreach (var tool in snapshot.Tools.Value ?? [])
            text.AppendLine($"{tool.Id}：观察到运行进程候选，版本和产品形态尚未核实。");
        text.AppendLine("未观察到进程不代表未安装。未知版本不开放修复。");
        var inventory = ToolInventory.Build(snapshot.Tools.Value ?? [], catalogInstalled, true, catalogMetadataComplete);
        CatalogGrid.ItemsSource = inventory;
        text.AppendLine("\n【截图软件清单与Codex · 21项】");
        foreach (var row in inventory)
            text.AppendLine($"{row.Name}　{row.Category}　{row.Detection}　版本候选：{row.Versions}\n  {row.Capability}；{row.Note}");
        text.AppendLine("\n【安装元数据与配置来源】");
        foreach (var tool in installedCandidates)
            text.AppendLine($"{tool.Id}　候选版本：{tool.Version ?? "未知"}　{tool.Form}（不证明可执行文件可信）");
        foreach (var config in configurations)
        {
            text.AppendLine($"{config.ToolId}：{Label(config.Status)}　{config.ErrorCode}");
            foreach (var source in config.Sources ?? [])
            {
                text.AppendLine($"  {source.Scope}：{Label(source.Status)}　{source.ErrorCode}");
                foreach (var field in source.Inspection?.ProxyFields ?? new Dictionary<string, string>())
                    text.AppendLine($"    {field.Key}：{field.Value}");
                if (source.Inspection?.ProviderConfigured == true) text.AppendLine("    观察到提供商配置（不导出Key，不更换提供商）。");
            }
        }
        text.AppendLine("候选缺失不代表未安装。当前不扫描会话库；进程/项目/托管覆盖链未完全核实。");
        text.AppendLine("WorkBuddy：可在应用“设置→系统设置→网络”核实直连/跟随系统/手动代理；磁盘结构尚未核实，不读取CodeBuddy配置替代。");
        text.AppendLine("\n【判断与下一步】");
        foreach (var finding in new DiagnosticEngine().Analyze(snapshot, configurations, []))
            text.AppendLine($"{finding.Explanation}\n下一步：{finding.NextStep}");
        text.AppendLine("本报告未验证 Agent 登录、API 调用成功或 VPN 退出后可用性；没有执行修复。");
        ReportBox.Text = Redactor.Redact(text.ToString());
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "文本报告 (*.txt)|*.txt", FileName = "agent-network-report.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, ReportBox.Text, new UTF8Encoding(false)); StatusText.Text = "已导出当前可见报告"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, "报告保存失败，请检查目标目录权限或磁盘空间。", "导出失败"); }
    }

    private void BeginOperation()
    {
        operation = new();
        CollectButton.IsEnabled = ExportButton.IsEnabled = BasicStartButton.IsEnabled = HomeToolBox.IsEnabled = ToolNetworkButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
    }

    private void EndOperation()
    {
        operation?.Dispose(); operation = null;
        CollectButton.IsEnabled = true;
        BasicStartButton.IsEnabled = HomeToolBox.IsEnabled = ToolNetworkButton.IsEnabled = true;
        ExportButton.IsEnabled = snapshot is not null;
        CancelButton.IsEnabled = false;
    }

    private void ShowFailure(Exception ex)
    {
        session.Fail();
        StatusText.Text = "操作未完成";
        MessageBox.Show(this, $"检查未完成（{ex.GetType().Name}）。没有修改网络配置。", "检查失败");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void BasicStart_Click(object sender, RoutedEventArgs e)
    {
        var id = HomeToolBox.SelectedValue as string ?? "codex";
        new ToolNetworkWindow(id, true) { Owner = this }.ShowDialog();
        StatusText.Text = "检查窗口已关闭，可以选择其他软件继续检查。";
    }
    private void ToolNetwork_Click(object sender, RoutedEventArgs e)
    {
        var selected = CatalogGrid.SelectedItem as ToolInventoryRow;
        var id = ToolCatalog.All.FirstOrDefault(tool => tool.Name == selected?.Name)?.Id ?? "codex";
        new ToolNetworkWindow(id) { Owner = this }.ShowDialog();
    }
    private void Window_Closing(object? sender, CancelEventArgs e) => operation?.Cancel();
    private static string Label(ObservationStatus status) => status switch
    {
        ObservationStatus.Success => "已取得结果",
        ObservationStatus.Partial => "部分取得",
        ObservationStatus.PermissionDenied => "权限不足",
        ObservationStatus.NotTested => "未测试",
        ObservationStatus.NotApplicable => "不适用",
        ObservationStatus.UnsupportedVersion => "不支持的版本",
        _ => "失败"
    };
}
