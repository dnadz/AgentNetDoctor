using AgentNetDoctor.Core.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
namespace AgentNetDoctor.Core.Networking;

public sealed record NetworkStage(string Stage, ObservationStatus Status, string Detail, double Milliseconds);
public static class ServiceDiagnostics
{
    public static Uri ParseEndpoint(string text)
    {
        if (text.Length > 2048 || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            !(uri.Scheme == "https" || uri.Scheme == "http" && IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)) ||
            !(uri.AbsolutePath == "/" || uri.AbsolutePath == "/api/version" && IPAddress.TryParse(uri.Host.Trim('[', ']'), out var local) && IPAddress.IsLoopback(local)))
            throw new ArgumentException("请输入不含凭据/查询参数的HTTPS服务根地址，或本机Ollama的http://127.0.0.1:端口/api/version。");
        return uri;
    }
    public static async Task<IReadOnlyList<NetworkStage>> InspectDirectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        endpoint = ParseEndpoint(endpoint.AbsoluteUri);
        List<NetworkStage> result = [];
        var clock = Stopwatch.StartNew();
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(endpoint.DnsSafeHost, timeout.Token);
                result.Add(new("本机DNS/地址解析", addresses.Length > 0 ? ObservationStatus.Success : ObservationStatus.Failed, $"返回{addresses.Length}个地址；显式代理可能使用远端DNS", clock.Elapsed.TotalMilliseconds));
            }
            catch (SocketException error) { result.Add(new("本机DNS/地址解析", ObservationStatus.Failed, error.SocketErrorCode.ToString(), clock.Elapsed.TotalMilliseconds)); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { result.Add(new("本机DNS/地址解析", ObservationStatus.Failed, "DNS_TIMEOUT", clock.Elapsed.TotalMilliseconds)); }
        }
        clock.Restart();
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        using (var tcp = new TcpClient())
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                await tcp.ConnectAsync(endpoint.DnsSafeHost, endpoint.Port, timeout.Token);
                result.Add(new("无显式代理TCP", ObservationStatus.Success, "端口接受连接；仍可能经过TUN，不证明服务身份或业务", clock.Elapsed.TotalMilliseconds));
            }
            catch (SocketException error) { result.Add(new("无显式代理TCP", ObservationStatus.Failed, error.SocketErrorCode.ToString(), clock.Elapsed.TotalMilliseconds)); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { result.Add(new("无显式代理TCP", ObservationStatus.Failed, "TCP_TIMEOUT", clock.Elapsed.TotalMilliseconds)); }
        }
        return result;
    }
}
