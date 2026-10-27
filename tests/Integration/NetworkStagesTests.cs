using AgentNetDoctor.Windows.Networking;
using AgentNetDoctor.Core.Models;
using System.Net;
using System.Net.Sockets;

namespace AgentNetDoctor.IntegrationTests;

public sealed class NetworkStagesTests
{
    [Theory]
    [InlineData("https://user:pass@example.test/")]
    [InlineData("https://example.test/?key=secret")]
    [InlineData("http://192.0.2.1/")]
    [InlineData("https://example.test/v1/chat/completions")]
    public void UnsafeOrBusinessOperationEndpointCannotBeRequested(string address) => Assert.Throws<ArgumentException>(() => NetworkStages.ParseEndpoint(address));
    [Fact]
    public async Task RealLoopbackDnsAndTcpStagesAreObservedWithoutCallingModel()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var stages = await NetworkStages.InspectDirectAsync(new Uri($"http://127.0.0.1:{port}/"), CancellationToken.None);
            Assert.Equal(2, stages.Count);
            Assert.All(stages, stage => Assert.Equal(ObservationStatus.Success, stage.Status));
        }
        finally { listener.Stop(); }
    }
}
