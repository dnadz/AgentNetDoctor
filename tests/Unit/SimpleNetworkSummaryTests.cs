using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;

namespace AgentNetDoctor.UnitTests;

public sealed class SimpleNetworkSummaryTests
{
    [Fact]
    public void LocalCheckCannotClaimServerOrBusinessHealthy()
    {
        var analysis = new ToolNetworkAnalysis("codex", 1, [], []);
        var result = SimpleNetworkSummary.Create("Codex", analysis, false, []);
        Assert.Contains("尚未测试软件服务器", result.Detail);
        Assert.DoesNotContain("网络正常", result.Title);
        var unknown = SimpleNetworkSummary.Create("Codex", analysis with { CandidateProcessCount = 0 }, false, []);
        Assert.Contains("不表示没有安装", unknown.Detail);
    }
    [Fact]
    public void ProxyAuthenticationAndStoppedProxyProduceDifferentNextSteps()
    {
        var analysis = new ToolNetworkAnalysis("codex", 1, [], []);
        var now = DateTimeOffset.UtcNow;
        var auth = new ProbeResult("fixture", ProbePath.ExplicitProxy, "s", new(ObservationStatus.Success, 407, null, now), TimeSpan.Zero);
        Assert.Contains("登录或授权", SimpleNetworkSummary.Create("Codex", analysis, false, [auth]).Title);
        Assert.Contains("需要核对", SimpleNetworkSummary.Create("Codex", analysis, true, []).Title);
    }
}
