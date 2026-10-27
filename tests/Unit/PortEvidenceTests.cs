using AgentNetDoctor.Core.Diagnostics;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.UnitTests;

public sealed class PortEvidenceTests
{
    private static readonly DateTimeOffset Started = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void SamePidWithDifferentStartTimeIsNotSameProcess()
    {
        var original = new ProcessIdentity(42, Started, @"C:\synthetic\agent.exe");
        var owner = new Observation<ProcessIdentity>(ObservationStatus.Success, original, null, Started);
        Assert.False(PortEvidence.MatchesProcess(owner, original with { StartedAt = Started.AddMinutes(1) }));
        Assert.True(PortEvidence.MatchesProcess(owner, original));
    }

    [Fact]
    public void PermissionDeniedDoesNotConfirmOwnership()
    {
        var process = new ProcessIdentity(42, Started, @"C:\synthetic\agent.exe");
        var denied = new Observation<ProcessIdentity>(ObservationStatus.PermissionDenied, null, "ACCESS_DENIED", Started);
        Assert.False(PortEvidence.MatchesProcess(denied, process));
    }
}
