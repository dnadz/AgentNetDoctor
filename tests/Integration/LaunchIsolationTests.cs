using AgentNetDoctor.Core.Launch;
using AgentNetDoctor.Windows.Launch;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace AgentNetDoctor.IntegrationTests;

public sealed class LaunchIsolationTests
{
    [Theory]
    [InlineData(LaunchNetworkMode.ClearProxyEnvironment)]
    [InlineData(LaunchNetworkMode.ExplicitHttpProxy)]
    public async Task OnlyChildReceivesSelectedProxyEnvironment(LaunchNetworkMode mode)
    {
        var fixture = new RepairFixture();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "owned-crash-fixture"), "synthetic test only");
        var executable = Path.Combine(AppContext.BaseDirectory, "crash-host", "RepairCrashHost.exe");
        var fingerprint = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable)));
        var profile = new LaunchProfile("synthetic-child", "1", fingerprint, mode, mode == LaunchNetworkMode.ExplicitHttpProxy ? "http://127.0.0.1:9090" : null);
        var before = Environment.GetEnvironmentVariables();
        var launcher = new TargetLauncher([new("synthetic-child", "1", executable, fixture.Root, ["observe", fixture.Root], [mode], () => "1", () => true)]);
        var result = await launcher.StartAsync(profile, CancellationToken.None);
        Assert.Equal(LaunchState.StartedNeedsVerification, result.State);
        // Exercise delayed parent scheduling; the child must wait for our release signal.
        await Task.Delay(200);
        using var child = Process.GetProcessById(result.ProcessId!.Value);
        _ = child.SafeHandle; // Retain the actual process handle before allowing the child to exit.
        Assert.False(child.HasExited);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "release-child"), "release");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!child.HasExited) child.Kill(); throw; }
        Assert.Equal(0, child.ExitCode);
        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(await File.ReadAllTextAsync(Path.Combine(fixture.Root, "observed.json")))!;
        Assert.Equal(profile.Proxy, values["HTTP_PROXY"]);
        Assert.Equal(profile.Proxy, values["HTTPS_PROXY"]);
        Assert.Null(values["ALL_PROXY"]);
        Assert.Null(values["NO_PROXY"]);
        foreach (var key in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY" })
            Assert.Equal(before[key], Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public async Task ExistingInstanceUnknownTargetAndChangedBinaryAreRejected()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "crash-host", "RepairCrashHost.exe");
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable)));
        var profile = new LaunchProfile("synthetic-child", "1", hash, LaunchNetworkMode.ClearProxyEnvironment, null);
        var stopped = false;
        var launcher = new TargetLauncher([new("synthetic-child", "1", executable, AppContext.BaseDirectory, [], [profile.Mode], () => "1", () => stopped)]);
        Assert.Equal(LaunchState.NeedsUserExit, (await launcher.StartAsync(profile, CancellationToken.None)).State);
        stopped = true;
        Assert.Equal(LaunchState.Rejected, (await launcher.StartAsync(profile with { ExecutableFingerprint = new string('0', 64) }, CancellationToken.None)).State);
        Assert.Equal(LaunchState.Rejected, (await launcher.StartAsync(profile with { TargetId = "unregistered" }, CancellationToken.None)).State);
    }
}
