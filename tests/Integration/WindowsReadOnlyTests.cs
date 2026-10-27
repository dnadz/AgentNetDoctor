using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Windows.Collection;

namespace AgentNetDoctor.IntegrationTests;

public sealed class WindowsReadOnlyTests
{
    [Fact]
    public async Task CollectsWithoutClaimingFullNetworkHealth()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var snapshot = await new WindowsSnapshotCollector().CollectAsync(timeout.Token);
        Assert.Equal(ObservationStatus.Partial, snapshot.Status);
        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.NotEqual(ObservationStatus.Success, snapshot.Tools.Status);
        Assert.NotEqual(ObservationStatus.Success, snapshot.UserProxy.Status);
    }
}
