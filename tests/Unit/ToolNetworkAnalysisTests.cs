using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;

namespace AgentNetDoctor.UnitTests;

public sealed class ToolNetworkAnalysisTests
{
    [Fact]
    public void AllTwentyOneHaveRealAnalysisAndConnectionsAreBoundToObservedPidAndStartTime()
    {
        var now = DateTimeOffset.UtcNow;
        var identity = new ProcessIdentity(42, now, "fixture.exe");
        var other = identity with { StartedAt = now.AddSeconds(-1) };
        var snapshot = new NetworkSnapshot("s", 1, now, new(ObservationStatus.Success, new(false, null, false, false), null, now),
            new(ObservationStatus.Partial, [new("127.0.0.1", 10001, "ESTABLISHED", new(ObservationStatus.Success, identity, null, now)),
                new("127.0.0.1", 10002, "ESTABLISHED", new(ObservationStatus.Success, other, null, now))], null, now),
            new(ObservationStatus.Partial, [new("cursor", "candidate", null, identity)], null, now), ObservationStatus.Partial);
        foreach (var tool in ToolCatalog.All) Assert.NotEmpty(ToolNetworkAnalyzer.Analyze(tool.Id, snapshot, []).Actions);
        var result = ToolNetworkAnalyzer.Analyze("cursor", snapshot, []);
        Assert.Equal(1, result.CandidateProcessCount);
        Assert.Equal(10001, Assert.Single(result.Connections).Port);
        Assert.Empty(ToolNetworkAnalyzer.Analyze("qoder", snapshot, []).Connections);
    }
    [Fact]
    public void UnknownSoftwareIsRejectedRatherThanAssumingNetworkSupport() =>
        Assert.Throws<ArgumentException>(() => ToolNetworkAnalyzer.Analyze("unknown", null!, []));

    [Fact]
    public void DifferentServiceOrOnlyOnePathMustNotGenerateTwoPathOptimization()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new NetworkSnapshot("s", 1, now, new(ObservationStatus.Success, null, null, now),
            new(ObservationStatus.Partial, [], null, now), new(ObservationStatus.Partial, [], null, now), ObservationStatus.Partial);
        var direct = new ProbeResult("cursor:one:443", ProbePath.NoExplicitProxy, "s", new(ObservationStatus.Failed, null, "fixture", now), TimeSpan.Zero);
        var proxy = new ProbeResult("cursor:two:443", ProbePath.ExplicitProxy, "s", new(ObservationStatus.Success, 200, null, now), TimeSpan.Zero);
        Assert.DoesNotContain(ToolNetworkAnalyzer.Analyze("cursor", snapshot, [direct, proxy]).Actions, action => action.Contains("优先在本软件内部选择"));
        Assert.DoesNotContain(ToolNetworkAnalyzer.Analyze("cursor", snapshot, [direct]).Actions, action => action.Contains("两条已测路径均失败"));
    }
    [Theory]
    [InlineData("cursor", "api.example.test", 407)]
    [InlineData("ollama", "127.0.0.1", 200)]
    public void ProxyAuthenticationOrLocalServiceCannotRecommendProxySwitch(string id, string host, int status)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new NetworkSnapshot("s", 1, now, new(ObservationStatus.Success, null, null, now),
            new(ObservationStatus.Partial, [], null, now), new(ObservationStatus.Partial, [], null, now), ObservationStatus.Partial);
        var key = id + ":" + host + ":11434";
        ProbeResult[] probes = [new(key, ProbePath.NoExplicitProxy, "s", new(ObservationStatus.Failed, null, "fixture", now), TimeSpan.Zero),
            new(key, ProbePath.ExplicitProxy, "s", new(ObservationStatus.Success, status, null, now), TimeSpan.Zero)];
        var result = ToolNetworkAnalyzer.Analyze(id, snapshot, probes);
        Assert.DoesNotContain(result.Actions, action => action.Contains("优先在本软件内部选择已测试的代理"));
        Assert.Contains(result.Actions, action => status == 407 ? action.Contains("407") : action.Contains("本机服务"));
    }
}
