using AgentNetDoctor.Core.Repair;
using AgentNetDoctor.Windows.Repair;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace AgentNetDoctor.App;

public partial class ProxyRepairWindow : Window
{
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentNetDoctor", "user-proxy-repairs");
    private ManualProxyPlan? plan;
    private bool busy;
    private sealed record RecoveryItem(string Id, string Label);
    public ProxyRepairWindow() => InitializeComponent();
    private ManualProxyRepair Repair() => new(new WindowsManualProxySettings(), new LoopbackEndpointCheck(), new ProtectedBackupStore(root));
    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (VpnOffBox.IsChecked != true) { DetailsBox.Text = "请先正常退出 VPN / 代理客户端，并确认本机端口检查范围。"; return; }
        SetBusy(true); plan = null;
        try
        {
            plan = await Repair().PrepareAsync(CancellationToken.None);
            DetailsBox.Text = plan is null ? "未生成可执行修复。可能代理未开启、端口仍有服务、超时/权限不足、使用 PAC/自动发现/协议分流，或有策略影响。\n\n请在 Windows 设置→网络和 Internet→代理核实；不要重置所有网络。没有修改任何设置。" :
                $"检测到已停止的本机静态手动代理：{plan.Before.Server}\n\n修改前：手动代理开启\n修改后：手动代理关闭，允许默认连接\n\n保留：代理地址、绕过列表、Agent 配置、VPN 配置、DNS、路由和 WinHTTP。\n影响：当前 Windows 用户中跟随系统代理的多个应用。\n执行前复核端口和当前设置；有效期2分钟。\n\n不证明外网 Agent 可用，已有进程可能需要正常重启。实验功能，尚未经干净VM写入验收。";
            StateText.Text = plan is null ? "不满足自动修复条件" : "差异已生成，等待确认";
        }
        catch (Exception ex) { Failure(ex); }
        finally { SetBusy(false); }
    }
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (plan is null) return;
        if (!WindowsManualProxySettings.WritesValidated) { StateText.Text = "当前包仅支持预览，系统代理写入尚未解锁"; return; }
        var reviewed = plan;
        if (MessageBox.Show(this, DetailsBox.Text + "\n\n是否执行此实验性修改？将先保存受保护恢复记录。", "确认当前用户网络影响", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true); plan = null;
        try
        {
            _ = new ProtectedBackupStore(root).PrivateDirectoryPath();
            if (Directory.EnumerateFiles(root, "*.bin").Take(201).Count() >= 200)
                throw new IOException("恢复记录达到200条上限。请先备份并人工管理记录，不会自动删除。");
            var id = await Repair().ApplyAsync(reviewed, ManualProxyRepair.Digest(reviewed), CancellationToken.None);
            StateText.Text = "设置已修改，业务连接待验证";
            DetailsBox.Text = $"已关闭当前用户手动系统代理，并通知Windows刷新设置。\n\n恢复记录：{id}\n\n请在原本不需要VPN的Agent中发起新连接；如有影响，可从恢复记录回滚。没有修改TUN、DNS或路由，现有程序可能需要保存任务并正常重启。";
            RefreshHistorySafely();
        }
        catch (Exception ex) { Failure(ex); RefreshHistorySafely(); }
        finally { SetBusy(false); }
    }
    private void History_Click(object sender, RoutedEventArgs e) => RefreshHistorySafely();
    private void RefreshHistorySafely()
    {
        try { LoadHistory(); }
        catch { StateText.Text = "恢复目录无法安全读取；未执行新的修改，请检查权限与路径"; }
    }
    private void LoadHistory()
    {
        RecordsBox.Items.Clear();
        // Validate the owned private directory before enumerating; record content is only opened on selection.
        _ = new ProtectedBackupStore(root).PrivateDirectoryPath();
        foreach (var file in Directory.EnumerateFiles(root, "*.bin").OrderByDescending(File.GetLastWriteTimeUtc).Take(200))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (Guid.TryParseExact(id, "N", out _)) RecordsBox.Items.Add(new RecoveryItem(id, $"{File.GetLastWriteTime(file):yyyy-MM-dd HH:mm:ss}　{id}"));
        }
    }
    private async void Rollback_Click(object sender, RoutedEventArgs e)
    {
        if (RecordsBox.SelectedItem is not RecoveryItem selected) { StateText.Text = "请先选择恢复记录"; return; }
        SetBusy(true); plan = null;
        try
        {
            var repair = Repair();
            var record = await repair.ReadRecordAsync(selected.Id, CancellationToken.None);
            var state = repair.Inspect(record);
            DetailsBox.Text = state + "\n\n回滚将恢复本次修复前的手动代理开关；目标代理若仍未启动，可能再次影响连接。仅当前设置仍与本次修改后完整匹配时才执行。";
            if (!WindowsManualProxySettings.WritesValidated) { StateText.Text = "已读取状态，当前包未解锁系统代理写入或回滚"; return; }
            if (MessageBox.Show(this, DetailsBox.Text + "\n\n确认回滚？", "确认恢复原设置", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            StateText.Text = await repair.RollbackAsync(selected.Id, CancellationToken.None) ? "已恢复原设置，业务连接待验证" : "当前设置已变化或未修改，拒绝覆盖";
        }
        catch (Exception ex) { Failure(ex); }
        finally { SetBusy(false); }
    }
    private void SetBusy(bool value)
    {
        busy = value;
        CheckButton.IsEnabled = HistoryButton.IsEnabled = RollbackButton.IsEnabled = VpnOffBox.IsEnabled = RecordsBox.IsEnabled = !value;
        ApplyButton.IsEnabled = !value && plan is not null && WindowsManualProxySettings.WritesValidated;
    }
    private void Failure(Exception error)
    {
        StateText.Text = "操作未完成，请检查恢复记录";
        DetailsBox.Text = $"操作未完成（{error.GetType().Name}）。\n\n若已点击执行，设置可能已修改，请读取恢复记录核对。不会自动覆盖或重复执行。";
    }
    private void Window_Closing(object? sender, CancelEventArgs e) { if (busy) { e.Cancel = true; StateText.Text = "正在完成当前操作，请稍候再关闭"; } }
}
