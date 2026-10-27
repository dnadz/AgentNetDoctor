using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using System.Diagnostics;
using System.Net;

namespace AgentNetDoctor.Core.Networking;

public sealed class HttpProbeRunner : IProbeRunner
{
    public async Task<ProbeResult> RunAsync(ProbeRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Endpoint.Uri, false);
        if (request.Path == ProbePath.ExplicitProxy)
        {
            if (request.ProxyUri is null) throw new ArgumentException("显式代理需要代理地址");
            Validate(request.ProxyUri, true);
        }
        if (request.Path == ProbePath.ProcessDefault)
            return Result(ObservationStatus.NotTested, null, "PROCESS_DEFAULT_NOT_IMPLEMENTED", TimeSpan.Zero);
        var clock = Stopwatch.StartNew();
        using var handler = new SocketsHttpHandler
        {
            UseProxy = request.Path == ProbePath.ExplicitProxy,
            Proxy = request.Path == ProbePath.ExplicitProxy ? new WebProxy(request.ProxyUri!) { BypassProxyOnLocal = false } : null,
            AllowAutoRedirect = false,
            UseCookies = false,
            Credentials = null,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            MaxResponseHeadersLength = 32
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Endpoint.Uri);
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            // HTTP failure statuses still establish reachability, not authentication or business health.
            return Result(ObservationStatus.Success, (int)response.StatusCode, null, clock.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Result(ObservationStatus.Failed, null, "PROBE_TIMEOUT", clock.Elapsed); }
        catch (HttpRequestException ex)
        { return Result(ObservationStatus.Failed, null, $"HTTP_{ex.HttpRequestError.ToString().ToUpperInvariant()}", clock.Elapsed); }

        ProbeResult Result(ObservationStatus status, int? code, string? error, TimeSpan elapsed) =>
            new(request.Endpoint.Id, request.Path, request.SnapshotId,
                new(status, code, error, DateTimeOffset.UtcNow), elapsed);
    }

    private static void Validate(Uri uri, bool proxy)
    {
        if (!uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("不允许包含凭据或片段的地址");
        if (proxy)
        {
            if (uri.Scheme != "http" || uri.AbsolutePath != "/" || uri.Query.Length != 0)
                throw new ArgumentException("当前仅支持无凭据HTTP代理地址");
        }
        else if (uri.Scheme != "https" && !(uri.Scheme == "http" && IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address)))
            throw new ArgumentException("目标必须为HTTPS，回环测试除外");
        if (uri.Query.Length != 0) throw new ArgumentException("探测不接受查询参数");
    }
}
