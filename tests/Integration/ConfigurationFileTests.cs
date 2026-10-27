using AgentNetDoctor.Adapters;
using AgentNetDoctor.Core.Models;
using System.Diagnostics;
using AgentNetDoctor.Windows.Repair;

namespace AgentNetDoctor.IntegrationTests;

public sealed class ConfigurationFileTests
{
    [Fact]
    public async Task JunctionIsRejectedBeforeReadingItsTarget()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "synthetic-junction-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        Directory.CreateDirectory(target);
        try
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("New-Item -ItemType Junction -Path $env:AGENT_TEST_LINK -Target $env:AGENT_TEST_TARGET -ErrorAction Stop | Out-Null");
            start.Environment["AGENT_TEST_LINK"] = link;
            start.Environment["AGENT_TEST_TARGET"] = target;
            using var process = Process.Start(start)!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw; }
            Assert.Equal(0, process.ExitCode);
            var result = await ConfigurationFileReader.ReadAsync("codex", "synthetic", Path.Combine(link, "config.toml"), CancellationToken.None);
            Assert.Equal("CONFIG_REPARSE_PATH_BLOCKED", result.ErrorCode);
            await Assert.ThrowsAsync<IOException>(() => new ProtectedBackupStore(Path.Combine(link, "backups"))
                .SaveAsync("fixture", [1, 2, 3], CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(target);
            Directory.Delete(root);
        }
    }
    [Fact]
    public async Task RemotePathIsRejectedBeforeAnyFileOpen()
    {
        var result = await ConfigurationFileReader.ReadAsync("codex", "synthetic", @"\\example.invalid\share\config.toml", CancellationToken.None);
        Assert.Equal(ObservationStatus.NotTested, result.Status);
        Assert.Equal("CONFIG_REMOTE_OR_DEVICE_PATH_BLOCKED", result.ErrorCode);
    }
    [Fact]
    public async Task MissingAndMalformedConfigurationStayDistinctAndOriginalBytesUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "AgentNetDoctor-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json");
        try
        {
            var missing = await ConfigurationFileReader.ReadAsync("claude-code", "synthetic", path, CancellationToken.None);
            Assert.Equal("CONFIG_NOT_PRESENT", missing.ErrorCode);
            var content = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("{broken")).ToArray();
            await File.WriteAllBytesAsync(path, content);
            var malformed = await ConfigurationFileReader.ReadAsync("claude-code", "synthetic", path, CancellationToken.None);
            Assert.Equal(ObservationStatus.Failed, malformed.Status);
            Assert.Equal("CONFIG_SYNTAX_INVALID", malformed.ErrorCode);
            Assert.Equal(content, await File.ReadAllBytesAsync(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(root); }
    }
}
