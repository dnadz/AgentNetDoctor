using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Windows.Collection;

namespace AgentNetDoctor.IntegrationTests;

public sealed class NetworkEnvironmentTests
{
    [Fact]
    public void ReadOnlyEnvironmentActuallyAttemptsCollectionButDoesNotClaimEffectiveRoutes()
    {
        var result = NetworkEnvironmentReader.Read(CancellationToken.None);
        Assert.NotEqual(ObservationStatus.NotTested, result.Interfaces.Status);
        Assert.NotEqual(ObservationStatus.NotTested, result.WinHttp.Status);
        Assert.Equal(ObservationStatus.NotTested, result.EffectiveRoutesStatus);
        if (result.Interfaces.Value is { } interfaces)
            Assert.All(interfaces, item => Assert.NotNull(item.DnsServers));
    }
}
