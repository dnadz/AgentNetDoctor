using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Mac;

namespace AgentNetDoctor.MacTests;

public sealed class MacParserTests
{
    [Fact]
    public void ProxyParserKeepsPacAndOnlyNumericLoopbackPorts()
    {
        var result = MacParsers.ParseProxy("<dictionary> {\n HTTPEnable : 1\n HTTPProxy : 127.0.0.1\n HTTPPort : 7890\n HTTPSEnable : 1\n HTTPSProxy : proxy.example\n HTTPSPort : 8080\n ProxyAutoConfigEnable : 1\n ProxyAutoDiscoveryEnable : 1\n}");
        Assert.True(result.Settings.Enabled);
        Assert.True(result.Settings.PacConfigured);
        Assert.True(result.Settings.AutoDetect);
        Assert.Equal("http://127.0.0.1:7890/", Assert.Single(result.LoopbackEndpoints).AbsoluteUri);
    }

    [Fact]
    public void FailedOrMalformedProxyOutputIsNotReportedAsDisabled()
    {
        Assert.Throws<FormatException>(() => MacParsers.ParseProxy("permission denied"));
        Assert.Throws<FormatException>(() => MacParsers.ParseProxy("<dictionary> {\n HTTPEnable : 1\n HTTPProxy : 127.0.0.1\n HTTPPort : bad\n}"));
        Assert.False(MacParsers.ParseProxy("<dictionary> {\n}").Settings.Enabled);
    }

    [Fact]
    public void ProcessPathsWithSpacesKeepPidStartIdentityAndExcludeArguments()
    {
        var result = MacParsers.ParseProcesses("42 Tue Oct 27 12:13:14 2026 /Applications/Visual Studio Code.app/Contents/MacOS/Electron\n9 invalid\n");
        var process = Assert.Single(result).Value;
        Assert.Equal(42, process.Pid);
        Assert.EndsWith("/MacOS/Electron", process.ExecutablePath);
        Assert.Equal(2026, process.StartedAt.Year);
    }

    [Fact]
    public void LsofTracksDescriptorsIPv6RemoteAndUnknownOwner()
    {
        var identity = new ProcessIdentity(42, DateTimeOffset.UnixEpoch, "/Applications/Codex.app/Contents/MacOS/Codex");
        var ports = MacParsers.ParsePorts("p42\ncCodex\nf10\nn127.0.0.1:7890\nTST=LISTEN\nf11\nn[::1]:55000->[::1]:11434\nTST=ESTABLISHED\np99\nf1\nn*:9000\nTST=LISTEN\n", new Dictionary<int, ProcessIdentity> { [42] = identity });
        Assert.Equal(3, ports.Count);
        Assert.Equal(identity, ports[0].Owner.Value);
        Assert.Equal("::1", ports[1].RemoteAddress);
        Assert.Equal(11434, ports[1].RemotePort);
        Assert.Equal(ObservationStatus.Partial, ports[2].Owner.Status);
        Assert.Null(ports[2].Owner.Value);
    }
}
