using AgentNetDoctor.Core.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security;
using AgentNetDoctor.Core.Networking;

namespace AgentNetDoctor.Windows.Networking;


public sealed record ProxyEnvironmentValue(string Scope, string Name, string Value);
public static class NetworkStages
{
    public static IReadOnlyList<ProxyEnvironmentValue> ReadProxyEnvironment()
    {
        List<ProxyEnvironmentValue> values = [];
        foreach (var scope in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
            foreach (var key in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "OLLAMA_HOST", "CLOUDSDK_PROXY_TYPE", "CLOUDSDK_PROXY_ADDRESS", "CLOUDSDK_PROXY_PORT" })
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(key, scope);
                    if (string.IsNullOrEmpty(value)) continue;
                    var safe = key == "NO_PROXY" ? "已设置（绕过规则未导出）" :
                        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/" && uri.Scheme is "http" or "https" or "socks5" or "socks5h" ? uri.AbsoluteUri :
                        "已设置（格式或内容不导出，请在应用中核实）";
                    values.Add(new(scope.ToString(), key, safe));
                }
                catch (Exception error) when (error is SecurityException or UnauthorizedAccessException) { values.Add(new(scope.ToString(), key, "权限不足")); }
            }
        return values;
    }
    public static Uri ParseEndpoint(string text) => ServiceDiagnostics.ParseEndpoint(text);
    public static Task<IReadOnlyList<NetworkStage>> InspectDirectAsync(Uri endpoint, CancellationToken cancellationToken) => ServiceDiagnostics.InspectDirectAsync(endpoint, cancellationToken);
}
