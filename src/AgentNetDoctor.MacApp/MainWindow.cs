using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using AgentNetDoctor.Core.Reporting;
using AgentNetDoctor.Mac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using System.Text;

namespace AgentNetDoctor.MacApp;

public sealed class MainWindow : Window
{
    private readonly ComboBox tools = new() { Name = "ToolSelector", ItemsSource = ToolCatalog.All.Select(tool => tool.Name).ToArray(), SelectedIndex = 20, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button start = new() { Name = "StartCheck", Content = "开始检查", Padding = new Thickness(22, 10) };
    private readonly Button cancel = new() { Content = "停止检查", IsEnabled = false };
    private readonly TextBlock title = Text("准备检查", 22);
    private readonly TextBlock summary = Text("先正常打开遇到问题的软件，再点击开始检查。", 15);
    private readonly TextBlock next = Text("不需要填写网址或端口；不会自动修改网络设置。", 15);
    private readonly TextBlock status = Text("中文Mac试用版 · 检查与建议 · 退出后不常驻", 12);
    private readonly TextBox target = new() { Name = "ServiceAddress", PlaceholderText = "实际HTTPS服务根地址（不含密钥）" };
    private readonly TextBox proxy = new() { PlaceholderText = "可选：现有无凭据HTTP代理，如 http://127.0.0.1:7890" };
    private readonly CheckBox consent = new() { Name = "NetworkConsent", Content = "同意向填写的服务发送无认证测试请求，不调用模型" };
    private readonly Button test = new() { Name = "TestService", Content = "测试填写的服务", IsEnabled = false };
    private readonly Button export = new() { Content = "保存技术报告", IsEnabled = false };
    private readonly TextBox report = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200 };
    private MacInspection? inspection;
    private IReadOnlyList<NetworkStage> localStages = [];
    private IReadOnlyList<NetworkStage> serviceStages = [];
    private readonly List<ProbeResult> probes = [];
    private CancellationTokenSource? operation;
    private CatalogTool Tool => ToolCatalog.All[tools.SelectedIndex];
    public Expander Advanced { get; }

    public MainWindow()
    {
        Title = "Agent 网络医生 · Mac简单模式";
        Width = 940; Height = 760; MinWidth = 760; MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#EDF4F6"));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { start, cancel } };
        Advanced = new Expander
        {
            Name = "AdvancedOptions",
            Header = "高级选项与技术报告（需要时再打开）",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Text("需要先完成本机检查。联网测试需填写实际服务并同意；无显式代理仍可能经过VPN/TUN。HTTP响应不证明业务成功。", 13),
                    target, proxy, consent, test, export, report
                }
            }
        };
        var content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 18,
            Children =
            {
                Text("Agent 网络医生", 30), Text("软件连不上？先检查电脑端，再按提示操作。", 17),
                Text("1. 选择遇到问题的软件", 17), tools, Text("2. 开始检查", 17), buttons,
                new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(20),
                    Child = new StackPanel { Spacing = 12, Children = { title, summary, Text("下一步", 15), next } } },
                Text("清单中的Mac发行及形态仍需逐项核实；未发现运行候选不表示未安装。默认不请求公网、不读取聊天、API Key或私有配置。", 13),
                Advanced, status
            }
        };
        Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        start.Click += async (_, _) => await RunAsync(CheckLocalAsync);
        cancel.Click += (_, _) => operation?.Cancel();
        test.Click += async (_, _) => await RunAsync(TestServiceAsync);
        export.Click += async (_, _) => await SaveAsync();
        consent.IsCheckedChanged += (_, _) => UpdateControls();
        tools.SelectionChanged += (_, _) => Reset();
        Closed += (_, _) => operation?.Cancel();
        Reset();
    }

    private static TextBlock Text(string value, double size) => new() { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#173E4B")) };

    private void Reset()
    {
        inspection = null; probes.Clear(); localStages = []; serviceStages = []; consent.IsChecked = false;
        target.Text = Tool.Id == "ollama" ? "http://127.0.0.1:11434/api/version" : "";
        proxy.Text = ""; report.Text = "";
        title.Text = "准备检查 " + Tool.Name;
        summary.Text = "先正常打开这个软件。将检查当前权限可见的运行连接和系统代理；候选识别不证明发行来源或签名。";
        next.Text = "点击“开始检查”，无需填写网址或端口。";
        UpdateControls();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (operation is not null) return;
        using var running = new CancellationTokenSource(); operation = running;
        UpdateControls(); title.Text = "正在检查，请稍候…"; summary.Text = "采集结果尚不完整，暂不能判断连接是否正常。";
        try { await action(running.Token); running.Token.ThrowIfCancellationRequested(); Render(); }
        catch (OperationCanceledException) { title.Text = "检查已停止"; summary.Text = "未完成的部分不能作为网络正常的证据。"; next.Text = "需要时重新检查。"; }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException or System.Net.Sockets.SocketException)
        { title.Text = "这次检查未完成"; summary.Text = error is ArgumentException ? "服务地址格式不支持，请填写无凭据HTTPS根地址；本机Ollama可使用提示地址。" : "暂时未能取得完整结果，请稍后重试。"; next.Text = "没有修改系统设置。技术错误类型：" + error.GetType().Name; }
        finally { operation = null; UpdateControls(); }
    }

    private async Task CheckLocalAsync(CancellationToken token)
    {
        inspection = null; probes.Clear(); serviceStages = []; localStages = []; report.Text = "";
        var collected = await Task.Run(() => new MacSnapshotCollector().InspectAsync(token), token);
        var ports = await LocalProxyCheck.InspectAsync(collected.LocalProxyEndpoints, token);
        token.ThrowIfCancellationRequested();
        inspection = collected; localStages = ports;
    }

    private async Task TestServiceAsync(CancellationToken token)
    {
        if (inspection is null || consent.IsChecked != true) throw new InvalidOperationException("请先检查本机并同意联网");
        var endpoint = ServiceDiagnostics.ParseEndpoint(target.Text ?? "");
        Uri? proxyUri = null;
        if (!string.IsNullOrWhiteSpace(proxy.Text))
        {
            if (!Uri.TryCreate(proxy.Text.Trim(), UriKind.Absolute, out proxyUri) || proxyUri.Scheme != "http" || proxyUri.UserInfo.Length > 0 || proxyUri.Query.Length > 0 || proxyUri.Fragment.Length > 0 || proxyUri.AbsolutePath != "/")
                throw new ArgumentException("仅支持无凭据HTTP代理根地址");
        }
        probes.Clear(); serviceStages = [];
        var endpointId = Tool.Id + ":" + endpoint.Authority;
        var spec = new EndpointSpec(endpointId, endpoint, "用户指定实际服务");
        serviceStages = await ServiceDiagnostics.InspectDirectAsync(endpoint, token);
        var runner = new HttpProbeRunner();
        probes.Add(await runner.RunAsync(new(spec, ProbePath.NoExplicitProxy, null, inspection.Snapshot.Id), token));
        if (proxyUri is not null && !endpoint.IsLoopback)
            probes.Add(await runner.RunAsync(new(spec, ProbePath.ExplicitProxy, proxyUri, inspection.Snapshot.Id), token));
    }

    private void Render()
    {
        if (inspection is null) return;
        var snapshot = inspection.Snapshot;
        var analysis = ToolNetworkAnalyzer.Analyze(Tool.Id, snapshot, probes);
        var plain = SimpleNetworkSummary.Create(Tool.Name, analysis, localStages.Any(stage => stage.Status == ObservationStatus.Failed), probes);
        title.Text = plain.Title; summary.Text = plain.Detail; next.Text = plain.NextStep;
        if (snapshot.UserProxy.Value is null || snapshot.Tools.Value is null || snapshot.Ports.Value is null)
        { title.Text = "部分检查未能完成"; summary.Text = "代理、进程或TCP信息不完整；不能据此判断未安装、没有代理或网络正常。"; next.Text = "正常打开目标软件后重试，需要时保存报告。"; }
        StringBuilder text = new();
        text.AppendLine($"{Tool.Name} · Mac运行候选：{analysis.CandidateProcessCount} · 快照{snapshot.Id}");
        text.AppendLine($"系统代理：{snapshot.UserProxy.Status}；{snapshot.UserProxy.Value?.Server ?? "未知"}；PAC：{snapshot.UserProxy.Value?.PacConfigured.ToString() ?? "未知"}");
        text.AppendLine("关联TCP（最多80条；地址可能含隐私，分享前检查）：");
        foreach (var port in analysis.Connections.Take(80)) text.AppendLine($"{port.Address}:{port.Port} → {port.RemoteAddress}:{port.RemotePort} {port.State}");
        text.AppendLine("网卡/DNS配置（不证明实际解析与路由）：");
        foreach (var item in snapshot.Network?.Interfaces.Value ?? []) text.AppendLine($"{item.Name} {item.OperationalState} DNS:{string.Join(',', item.DnsServers)}");
        foreach (var variable in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "http_proxy", "https_proxy", "all_proxy", "no_proxy" })
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable))) text.AppendLine(variable + "已设置（仅本工具继承环境，原文不导出，不代表目标应用）");
        foreach (var note in inspection.Notes.Concat(analysis.Actions.Take(Math.Max(0, analysis.Actions.Count - 1)))) text.AppendLine(note);
        text.AppendLine(MacToolGuidance.For(Tool.Id));
        foreach (var stage in localStages.Concat(serviceStages)) text.AppendLine($"{stage.Stage}：{stage.Status} {stage.Detail}");
        foreach (var probeResult in probes) text.AppendLine($"HTTP {probeResult.Path}：{probeResult.HttpStatus.Status} {probeResult.HttpStatus.Value} {probeResult.HttpStatus.ErrorCode}");
        report.Text = Redactor.Redact(text.ToString());
        status.Text = "检查结束。未验证真实业务、应用私有代理或VPN退出时序；没有修改设置。";
    }

    private void UpdateControls()
    {
        var idle = operation is null;
        start.IsEnabled = tools.IsEnabled = target.IsEnabled = proxy.IsEnabled = consent.IsEnabled = idle;
        cancel.IsEnabled = !idle;
        test.IsEnabled = idle && inspection is not null && consent.IsChecked == true;
        export.IsEnabled = idle && inspection is not null;
    }

    private async Task SaveAsync()
    {
        if (inspection is null || operation is not null) return;
        var data = new ReportData(inspection.Snapshot.Id, [new("mac-diagnostics", ObservationStatus.Partial, report.Text, null)]);
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "保存报告（含网络地址，分享前请检查）", SuggestedFileName = "AgentNetDoctor-Mac-report.json", DefaultExtension = "json", ShowOverwritePrompt = true });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            await new ReportWriter().WriteAsync(data, stream, default);
            status.Text = "报告已保存。包含网络地址，分享前请检查。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { status.Text = "保存未完成，请选择可写位置。"; }
    }
}
