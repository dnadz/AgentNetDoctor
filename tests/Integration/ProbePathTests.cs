using System.Net;
using System.Net.Sockets;
using System.Text;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using AgentNetDoctor.Windows.Networking;

namespace AgentNetDoctor.IntegrationTests;

public sealed class ProbePathTests
{
    [Fact]
    public async Task ExplicitDirectDoesNotUseProxyAndDoesNotFollowRedirect()
    {
        await using var destination = new LocalHttpFixture(302);
        await using var proxy = new LocalHttpFixture(407);
        var request = new ProbeRequest(new("synthetic", destination.Uri, "合成夹具"),
            ProbePath.NoExplicitProxy, proxy.Uri, "snapshot");
        var result = await new ProbeRunner().RunAsync(request, CancellationToken.None);
        Assert.Equal(302, result.HttpStatus.Value);
        Assert.Equal(1, destination.Hits);
        Assert.Equal(0, proxy.Hits);
    }

    [Fact]
    public async Task ExplicitProxyUsesOnlyApprovedProxy()
    {
        await using var destination = new LocalHttpFixture(200);
        await using var proxy = new LocalHttpFixture(407);
        var request = new ProbeRequest(new("synthetic", destination.Uri, "合成夹具"),
            ProbePath.ExplicitProxy, proxy.Uri, "snapshot");
        var result = await new ProbeRunner().RunAsync(request, CancellationToken.None);
        Assert.Equal(407, result.HttpStatus.Value);
        Assert.Equal(1, proxy.Hits);
        Assert.Equal(0, destination.Hits);
    }

    [Fact]
    public async Task CredentialsInEndpointAreRejectedBeforeNetwork()
    {
        var request = new ProbeRequest(new("synthetic", new Uri("https://user:synthetic-password@example.invalid/"), "fixture"),
            ProbePath.NoExplicitProxy, null, "snapshot");
        await Assert.ThrowsAsync<ArgumentException>(() => new ProbeRunner().RunAsync(request, CancellationToken.None));
    }
}

internal sealed class LocalHttpFixture : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private int hits;
    private readonly Task server;
    public int Hits => Volatile.Read(ref hits);
    public Uri Uri { get; }

    public LocalHttpFixture(int status)
    {
        listener.Start();
        Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        server = Serve(status);
    }

    private async Task Serve(int status)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 }) { }
                Interlocked.Increment(ref hits);
                var response = $"HTTP/1.1 {status} Fixture\r\nContent-Length: 0\r\nConnection: close\r\nLocation: http://example.invalid/\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        await server;
        stop.Dispose();
    }
}
