using AgentNetDoctor.Core.Diagnostics;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.UnitTests;

public sealed class DiagnosticEngineTests
{
    [Fact]
    public void Http403DoesNotAssertCountryBlockOrPortConflict()
    {
        var time = DateTimeOffset.UtcNow;
        var snapshot = new NetworkSnapshot("synthetic", 1, time,
            new(ObservationStatus.PermissionDenied, null, "ACCESS_DENIED", time),
            new(ObservationStatus.NotTested, null, null, time),
            new(ObservationStatus.NotTested, null, null, time), ObservationStatus.Partial);
        var probe = new ProbeResult("synthetic-endpoint", ProbePath.NoExplicitProxy, snapshot.Id,
            new(ObservationStatus.Success, 403, null, time), TimeSpan.Zero);
        var findings = new DiagnosticEngine().Analyze(snapshot, [], [probe]);
        Assert.Contains(findings, f => f.RuleId == "HTTP_STATUS_REQUIRES_CONTEXT" && f.Attribution == Attribution.Unknown);
        Assert.Contains(findings, f => f.RuleId == "PROXY_OBSERVATION_INCOMPLETE");
        Assert.DoesNotContain(findings, f => f.RuleId is "PORT_CONFLICT" or "COUNTRY_BLOCK");
    }
}
