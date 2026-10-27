using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Networking;
using System.Net;
using System.Net.Sockets;

namespace AgentNetDoctor.Mac;

public static class LocalProxyCheck
{
    public static async Task<IReadOnlyList<NetworkStage>> InspectAsync(IEnumerable<Uri> endpoints, CancellationToken token)
    {
        List<NetworkStage> stages = [];
        foreach (var endpoint in endpoints.Distinct().Take(4))
        {
            if (!IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip))
                throw new ArgumentException("只能测试已配置的数字回环地址");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var client = new TcpClient(ip.AddressFamily);
            try
            {
                await client.ConnectAsync(ip, endpoint.Port, timeout.Token);
                stages.Add(new("已配置本机代理端口", ObservationStatus.Success, $"端口{endpoint.Port}接受连接；未验证HTTP代理协议", 0));
            }
            catch (SocketException error)
            {
                stages.Add(new("已配置本机代理端口", error.SocketErrorCode == SocketError.ConnectionRefused ? ObservationStatus.Failed : ObservationStatus.Partial,
                    $"端口{endpoint.Port}：{error.SocketErrorCode}；不据此自动改设置", 0));
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { stages.Add(new("已配置本机代理端口", ObservationStatus.Partial, "超时，结果未知", 0)); }
        }
        return stages;
    }
}
