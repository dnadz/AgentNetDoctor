using AgentNetDoctor.Adapters;
using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using AgentNetDoctor.Core.Reporting;
using AgentNetDoctor.Windows.Collection;
using AgentNetDoctor.Windows.Networking;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using AgentNetDoctor.Windows.Repair;

namespace AgentNetDoctor.App;

public partial class ToolNetworkWindow : Window
{
    private NetworkSnapshot? snapshot;
    private IReadOnlyList<ConfigurationSource> configurations = [];
    private IReadOnlyList<ProxyEnvironmentValue> environment = [];
    private IReadOnlyList<NetworkStage> stages = [];
    private IReadOnlyList<NetworkStage> localProxyStages = [];
    private readonly List<ProbeResult> probes = [];
    private CancellationTokenSource? operation;
    private CatalogTool Tool => (CatalogTool)ToolBox.SelectedItem;
    public ToolNetworkWindow(string selectedId = "codex", bool autoStart = false)
    {
        InitializeComponent();
        ToolBox.ItemsSource = ToolCatalog.All;
        ToolBox.SelectedValue = selectedId;
        if (autoStart) Loaded += AutoStart_Loaded;
    }
    private void AutoStart_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= AutoStart_Loaded;
        Local_Click(sender, e);
    }
    private void Tool_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ReportBox is null || ToolBox.SelectedItem is not CatalogTool tool) return;
        snapshot = null; probes.Clear(); stages = []; localProxyStages = []; configurations = []; environment = [];
        ToolHint.Text = tool.Note;
        TargetBox.Text = tool.Id switch { "ollama" => "http://127.0.0.1:11434/api/version", "github-desktop" => "https://api.github.com/", _ => "" };
        NetworkConsent.IsChecked = false;
        TestButton.IsEnabled = ExportButton.IsEnabled = false;
        ReportBox.Text = $"{tool.Name}：先检查本机进程、实际TCP和代理配置来源。\n\n{tool.Note}\n\n服务地址请从本软件实际设置取得；不会用官网可达证明业务健康。";
        SummaryTitle.Text = "准备检查 " + tool.Name;
        SummaryDetail.Text = "不需要填写网址或端口，先检查电脑端能够取得的信息。";
        SummaryNext.Text = "请先打开这个软件，再点击“开始检查”。";
        LocalButton.Content = "开始检查";
    }
    private async void Local_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        Begin(); var tool = Tool;
        snapshot = null; probes.Clear(); stages = []; localProxyStages = []; configurations = []; environment = [];
        try
        {
            var token = operation!.Token;
            var collected = await Task.Run(() => new WindowsSnapshotCollector().CollectAsync(token), token);
            var collectedEnvironment = NetworkStages.ReadProxyEnvironment();
            var collectedConfigurations = await ToolConfigurationCandidates.ReadAsync(tool.Id, token);
            var collectedProxyStages = await ReadLocalProxyPorts(collectedConfigurations, token);
            token.ThrowIfCancellationRequested();
            snapshot = collected;
            environment = collectedEnvironment;
            configurations = collectedConfigurations;
            localProxyStages = collectedProxyStages;
            Render(); StatusText.Text = "本机检查完成；可测试实际服务，不代表网络健康";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "检查已停止，尚未取得完整结果。";
            SummaryTitle.Text = "检查已停止";
            SummaryDetail.Text = "这次没有取得完整结果，不能据此判断网络好坏。";
            SummaryNext.Text = "需要时可重新开始检查。";
        }
        catch (Exception error) { Failure(error); }
        finally { End(); }
    }
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (operation is not null) return;
        if (snapshot is null) return;
        if (NetworkConsent.IsChecked != true) { StatusText.Text = "请先确认目标与联网范围"; return; }
        Uri target; Uri? proxy = null;
        try
        {
            target = NetworkStages.ParseEndpoint(TargetBox.Text);
            if (!string.IsNullOrWhiteSpace(ProxyBox.Text) && (!Uri.TryCreate(ProxyBox.Text.Trim(), UriKind.Absolute, out proxy) ||
                proxy.Scheme != "http" || proxy.UserInfo.Length > 0 || proxy.Query.Length > 0 || proxy.Fragment.Length > 0 || proxy.AbsolutePath != "/"))
                throw new ArgumentException("代理仅接受无凭据HTTP根地址。");
        }
        catch (ArgumentException error) { StatusText.Text = error.Message; return; }
        Begin(); probes.Clear(); stages = [];
        try
        {
            var token = operation!.Token;
            stages = await NetworkStages.InspectDirectAsync(target, token);
            var endpoint = new EndpointSpec(Tool.Id + ":" + target.Host + ":" + target.Port, target, "用户为所选软件指定的服务入口，不证明应用使用此地址");
            var runner = new ProbeRunner();
            probes.Add(await runner.RunAsync(new(endpoint, ProbePath.NoExplicitProxy, null, snapshot.Id), token));
            var localTarget = target.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(target.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
            if (proxy is not null && !localTarget) probes.Add(await runner.RunAsync(new(endpoint, ProbePath.ExplicitProxy, proxy, snapshot.Id), token));
            Render(); StatusText.Text = "测试与优化建议已生成；应用业务仍需验证，未修改网络";
        }
        catch (OperationCanceledException) { Render(); StatusText.Text = "已取消，已取得结果保留；其余未测试"; }
        catch (Exception error) { Failure(error); }
        finally { End(); }
    }
    private void Render()
    {
        if (snapshot is null) return;
        var analysis = ToolNetworkAnalyzer.Analyze(Tool.Id, snapshot, probes);
        var summary = SimpleNetworkSummary.Create(Tool.Name, analysis, localProxyStages.Any(stage => stage.Status == ObservationStatus.Failed), probes);
        SummaryTitle.Text = summary.Title;
        SummaryDetail.Text = summary.Detail;
        SummaryNext.Text = summary.NextStep;
        var text = new StringBuilder($"{Tool.Name} · 独立网络诊断与优化方案\n时间：{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm:ss zzz}（北京时间）\n\n");
        text.AppendLine($"【实际连接观察】\n关联进程身份数：{analysis.CandidateProcessCount}，TCP记录：{analysis.Connections.Count}");
        foreach (var port in analysis.Connections.Take(100))
            text.AppendLine($"PID {port.Owner.Value?.Pid}　{port.Address}:{port.Port} → {port.RemoteAddress ?? "未知"}:{port.RemotePort?.ToString() ?? "未知"}　{port.State}");
        text.AppendLine("仅关联PID、启动时间和路径同时匹配的候选；采集非原子，不能覆盖所有插件/后台/共享宿主。没有TCP记录不等于没有联网。最多显示100条。");
        text.AppendLine("\n【代理来源】");
        var proxy = snapshot.UserProxy.Value;
        text.AppendLine($"当前用户手动代理：{(proxy is null ? "未知" : proxy.Enabled ? "观察到开启" : "未观察到开启")}；PAC：{proxy?.PacConfigured.ToString() ?? "未知"}；自动发现：{proxy?.AutoDetect.ToString() ?? "未知"}");
        foreach (var item in environment) text.AppendLine($"{item.Scope}/{item.Name}：{item.Value}");
        foreach (var stage in localProxyStages) text.AppendLine($"{stage.Stage}：{stage.Status}　{stage.Detail}");
        text.AppendLine("Process是本检查软件的启动环境，不是目标进程内部环境；用户/机器环境不代表已启动应用已继承。未设置也不证明绕过TUN。不会读取目标进程内存。");
        text.AppendLine("\n【应用候选配置】");
        if (configurations.Count == 0) text.AppendLine("当前产品配置结构未验证，未读取未知数据库/会话文件；应用内代理需核对，不能标记已通过。");
        foreach (var config in configurations)
        {
            text.AppendLine($"{config.Scope}：{config.Status}　{config.ErrorCode}");
            foreach (var field in config.Inspection?.ProxyFields ?? new Dictionary<string, string>()) text.AppendLine($"{field.Key}：{field.Value}");
            if (config.Inspection?.HasProxyCredentials == true) text.AppendLine("观察到含凭据代理，地址未导出；不支持自动复制或改写。");
        }
        text.AppendLine("\n【服务分层与路径对比】");
        if (stages.Count == 0) text.AppendLine("尚未对指定服务执行DNS/TCP探测。");
        foreach (var stage in stages) text.AppendLine($"{stage.Stage}：{stage.Status}　{stage.Milliseconds:F0}ms　{stage.Detail}");
        foreach (var probe in probes) text.AppendLine($"{probe.EndpointId}　{(probe.Path == ProbePath.NoExplicitProxy ? "无显式代理" : "指定HTTP代理")}：{probe.HttpStatus.Status}　HTTP {probe.HttpStatus.Value?.ToString() ?? "无"}　{probe.HttpStatus.ErrorCode}　{probe.Elapsed.TotalMilliseconds:F0}ms");
        text.AppendLine("HTTPS请求按系统信任验证TLS；没有关闭证书校验。TLS未独立计时，HTTP错误码区分握手/解析等失败。TCP与HTTP是独立连接，不能据此保证使用相同IP；HTTP响应不等于认证/流式响应/推理业务成功。");
        text.AppendLine("\n【本软件优化步骤】");
        foreach (var action in analysis.Actions) text.AppendLine("• " + action);
        if (localProxyStages.Any(stage => stage.Status == ObservationStatus.Failed))
            text.AppendLine("• 本机候选代理端口明确拒绝连接：先核对本软件是否指向已退出的代理客户端；需要代理时恢复该入口，不需要代理时优先在该软件内部选择直连。不要同时覆盖其他工具或重置网络。");
        text.AppendLine("\n本次没有改写应用或系统配置。实际改动必须绑定已验证版本与差异，不能用通用修复覆盖21款软件。建议调整后先正常重启该软件，再按同一服务复测并正常关闭VPN验证直连目标。");
        ReportBox.Text = Redactor.Redact(text.ToString());
    }
    private static async Task<IReadOnlyList<NetworkStage>> ReadLocalProxyPorts(IReadOnlyList<ConfigurationSource> sources, CancellationToken cancellationToken)
    {
        List<(string Label, IPEndPoint Endpoint)> endpoints = [];
        List<NetworkStage> results = [];
        try
        {
            var settings = new WindowsManualProxySettings().Read();
            var system = AgentNetDoctor.Core.Repair.ManualProxyRepair.EligibleEndpoint(settings);
            if (system is not null) endpoints.Add(("当前用户静态代理端口（是否被应用采用待核实）", system));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        { results.Add(new("系统代理端口来源", ObservationStatus.Partial, "读取不完整，不能判定端口有效", 0)); }
        foreach (var source in sources)
            foreach (var field in source.Inspection?.ProxyFields ?? new Dictionary<string, string>())
                if (field.Key == "http.proxy" && Uri.TryCreate(field.Value, UriKind.Absolute, out var uri) && uri.Scheme == "http" &&
                    IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip))
                    endpoints.Add(("应用候选http.proxy端口", new(ip, uri.Port)));
        foreach (var endpoint in endpoints.Distinct().Take(4))
        {
            var observed = await new LoopbackEndpointCheck().CheckAsync(endpoint.Endpoint, cancellationToken);
            var status = observed switch { AgentNetDoctor.Core.Repair.LocalEndpointState.Accepting => ObservationStatus.Success, AgentNetDoctor.Core.Repair.LocalEndpointState.Refused => ObservationStatus.Failed, _ => ObservationStatus.Partial };
            results.Add(new(endpoint.Label, status, $"{endpoint.Endpoint}：{observed}；接受TCP不证明HTTP代理协议/认证正常，超时保持未知", 0));
        }
        return results;
    }
    private void Begin()
    {
        operation = new();
        ToolBox.IsEnabled = TargetBox.IsEnabled = ProxyBox.IsEnabled = NetworkConsent.IsEnabled = LocalButton.IsEnabled = TestButton.IsEnabled = ExportButton.IsEnabled = false;
        CancelButton.IsEnabled = true; StatusText.Text = "正在检查，请稍候…";
        SummaryTitle.Text = "正在检查，请稍候";
        SummaryDetail.Text = "正在收集本次检查结果，不会自动修改你的网络设置。";
        SummaryNext.Text = "完成后会在这里显示结果和下一步。";
    }
    private void End()
    {
        operation?.Dispose(); operation = null;
        ToolBox.IsEnabled = TargetBox.IsEnabled = ProxyBox.IsEnabled = NetworkConsent.IsEnabled = LocalButton.IsEnabled = true;
        TestButton.IsEnabled = ExportButton.IsEnabled = snapshot is not null;
        CancelButton.IsEnabled = false;
        LocalButton.Content = snapshot is not null ? "重新检查" : "开始检查";
    }
    private void Failure(Exception error)
    {
        StatusText.Text = "这次检查没有完成，没有修改设置。";
        SummaryTitle.Text = "这次检查没有完成";
        SummaryDetail.Text = "没有取得完整结果，暂时不能判断原因。";
        SummaryNext.Text = "请稍后重试。仍失败时，可展开高级选项查看或保存报告。";
        ReportBox.Text += $"\n检查错误类型：{error.GetType().Name}；未修改设置。";
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void Window_Closing(object? sender, CancelEventArgs e) { if (operation is not null) { operation.Cancel(); e.Cancel = true; StatusText.Text = "正在取消，请待操作结束后关闭"; } }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "文本报告 (*.txt)|*.txt", FileName = Tool.Id + "-network-report.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, ReportBox.Text, new UTF8Encoding(false)); StatusText.Text = "已导出当前可见网络报告"; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Failure(error); }
    }
}
