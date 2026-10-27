using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using AgentNetDoctor.Mac;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AgentNetDoctor.MacTests;

public sealed class MacProbeTests
{
    [Fact]
    public async Task NumericIpv6LoopbackPassesSharedSafetyValidation()
    {
        var endpoint = ServiceDiagnostics.ParseEndpoint("http://[::1]:11434/api/version");
        var result = await new HttpProbeRunner().RunAsync(new(new("ollama:fixture", endpoint, "只验证格式"), ProbePath.ProcessDefault, null, "fixture"), default);
        Assert.Equal(ObservationStatus.NotTested, result.HttpStatus.Status);
    }

    [Theory]
    [InlineData("https://user:secret@example.org/")]
    [InlineData("https://example.org/?key=secret")]
    [InlineData("http://192.0.2.1/")]
    [InlineData("https://example.org/v1/chat/completions")]
    public void ServiceEndpointsRejectCredentialsQueriesAndBusinessOperations(string address) => Assert.Throws<ArgumentException>(() => ServiceDiagnostics.ParseEndpoint(address));

    [Fact]
    public async Task LocalProxyTestCannotScanRemoteAddress()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => LocalProxyCheck.InspectAsync([new Uri("http://192.0.2.1:8080")], default));
    }

    [Fact]
    public async Task HttpRedirectIsNotFollowedAndNoCredentialsAreSent()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = socket.GetStream();
            var bytes = new byte[8192];
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(length), timeout.Token);
                if (count == 0) break;
                length += count;
                if (Encoding.ASCII.GetString(bytes, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
            }
            var request = Encoding.ASCII.GetString(bytes, 0, length);
            Assert.DoesNotContain("Authorization:", request, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cookie:", request, StringComparison.OrdinalIgnoreCase);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), timeout.Token);
        }, timeout.Token);
        var endpoint = ServiceDiagnostics.ParseEndpoint($"http://127.0.0.1:{port}/");
        var result = await new HttpProbeRunner().RunAsync(new(new("codex:fixture", endpoint, "合成回环"), ProbePath.NoExplicitProxy, null, "fixture"), timeout.Token);
        await server;
        Assert.Equal(302, result.HttpStatus.Value);
        Assert.Equal(ObservationStatus.Success, result.HttpStatus.Status);
    }
}
