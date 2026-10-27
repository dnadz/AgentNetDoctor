using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Mac;

namespace AgentNetDoctor.MacTests;

public sealed class MacCollectorTests
{
    private const string Process = "42 Tue Oct 27 12:13:14 2026 /Applications/Codex.app/Contents/MacOS/Codex\n";
    private const string Ports = "p42\ncCodex\nf10\nn127.0.0.1:50000->127.0.0.1:7890\nTST=ESTABLISHED\n";

    [Fact]
    public async Task FailedCollectionCannotPretendProxyDisabledOrSoftwareAbsent()
    {
        var inspection = await new MacSnapshotCollector(new FixtureRunner(true, false)).InspectAsync(default);
        Assert.NotEqual(ObservationStatus.Success, inspection.Snapshot.UserProxy.Status);
        Assert.Null(inspection.Snapshot.UserProxy.Value);
        Assert.Null(inspection.Snapshot.Tools.Value);
        Assert.Null(inspection.Snapshot.Ports.Value);
    }

    [Fact]
    public async Task PidReusedBetweenSnapshotsCannotOwnEarlierConnection()
    {
        var inspection = await new MacSnapshotCollector(new FixtureRunner(false, true)).InspectAsync(default);
        Assert.Empty(inspection.Snapshot.Tools.Value!);
        Assert.All(inspection.Snapshot.Ports.Value!, port => Assert.Null(port.Owner.Value));
        Assert.Equal(ObservationStatus.Partial, inspection.Snapshot.Status);
    }

    [Fact]
    public async Task StableCandidateOwnsConnectionButDoesNotClaimBusinessHealth()
    {
        var inspection = await new MacSnapshotCollector(new FixtureRunner(false, false)).InspectAsync(default);
        Assert.Equal("codex", Assert.Single(inspection.Snapshot.Tools.Value!).Id);
        Assert.Equal(42, Assert.Single(inspection.Snapshot.Ports.Value!).Owner.Value!.Pid);
        Assert.Equal(ObservationStatus.Partial, inspection.Snapshot.Status);
    }

    [Theory]
    [InlineData("/Applications/Visual Studio Code.app/Contents/MacOS/Electron", "vscode")]
    [InlineData("/Applications/Claude.app/Contents/MacOS/Claude", "claude-desktop")]
    [InlineData("/usr/local/bin/claude", null)]
    [InlineData("/usr/local/bin/node", null)]
    public void MatcherDoesNotTreatSharedHostsAsProducts(string path, string? id) => Assert.Equal(id, MacToolMatcher.Match(path)?.Id);

    private sealed class FixtureRunner(bool fail, bool replace) : IMacCommandRunner
    {
        private int calls;
        public Task<CommandResult> RunAsync(string command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = command switch
            {
                "proxy" => "<dictionary> {\n}",
                "ports" => Ports,
                "processes" when ++calls > 1 && replace => Process.Replace("12:13:14", "12:13:15"),
                "processes" => Process,
                _ => throw new InvalidOperationException()
            };
            return Task.FromResult(new CommandResult(fail ? 77 : 0, fail ? "permission denied" : output));
        }
    }
}
