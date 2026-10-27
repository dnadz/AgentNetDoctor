using AgentNetDoctor.Core.Models;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace AgentNetDoctor.Mac;

public sealed record MacProxy(ProxySettings Settings, IReadOnlyList<Uri> LoopbackEndpoints);
public static class MacParsers
{
    public static MacProxy ParseProxy(string text)
    {
        if (!text.TrimStart().StartsWith("<dictionary> {", StringComparison.Ordinal) || !text.TrimEnd().EndsWith('}'))
            throw new FormatException("SCUTIL_PROXY_FORMAT");
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        var depth = 0;
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(HTTPEnable|HTTPProxy|HTTPPort|HTTPSEnable|HTTPSProxy|HTTPSPort|SOCKSEnable|ProxyAutoConfigEnable|ProxyAutoDiscoveryEnable)\s*:\s*(.*?)\s*$");
            if (depth == 1 && match.Success && !values.TryAdd(match.Groups[1].Value, match.Groups[2].Value))
                throw new FormatException("SCUTIL_DUPLICATE_KEY");
            depth += line.Count(c => c == '{') - line.Count(c => c == '}');
        }
        if (depth != 0) throw new FormatException("SCUTIL_UNBALANCED");
        bool Flag(string key) => !values.TryGetValue(key, out var value) ? false : value switch
        { "0" => false, "1" => true, _ => throw new FormatException("SCUTIL_FLAG") };
        List<Uri> loopback = [];
        List<string> sources = [];
        foreach (var prefix in new[] { "HTTP", "HTTPS" })
        {
            if (!Flag(prefix + "Enable")) continue;
            if (!values.TryGetValue(prefix + "Proxy", out var host) || !values.TryGetValue(prefix + "Port", out var portText) ||
                !int.TryParse(portText, out var port) || port is < 1 or > 65535 || string.IsNullOrWhiteSpace(host))
                throw new FormatException("SCUTIL_PROXY_ENDPOINT");
            sources.Add(prefix + "代理已配置");
            if (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip))
                loopback.Add(new UriBuilder("http", ip.ToString(), port).Uri);
        }
        if (Flag("SOCKSEnable")) sources.Add("SOCKS代理已配置（协议未主动测试）");
        return new(new(sources.Count > 0, string.Join("；", sources), Flag("ProxyAutoConfigEnable"), Flag("ProxyAutoDiscoveryEnable")), loopback.Distinct().ToArray());
    }

    public static IReadOnlyDictionary<int, ProcessIdentity> ParseProcesses(string text)
    {
        Dictionary<int, ProcessIdentity> result = [];
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(\d+)\s+([A-Za-z]{3}\s+[A-Za-z]{3}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2}\s+\d{4})\s+(/.+?)\s*$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var pid) || pid <= 0) continue;
            var date = Regex.Replace(match.Groups[2].Value, @"\s+", " ");
            if (!DateTimeOffset.TryParseExact(date, "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var started)) continue;
            result.TryAdd(pid, new(pid, started, match.Groups[3].Value));
        }
        return result;
    }

    public static IReadOnlyList<PortRecord> ParsePorts(string text, IReadOnlyDictionary<int, ProcessIdentity> processes)
    {
        List<PortRecord> ports = [];
        int pid = 0;
        string? name = null;
        string state = "UNKNOWN";
        void Flush()
        {
            if (name is null) return;
            var pair = name.Split("->", StringSplitOptions.None);
            if (!TryAddress(pair[0], out var address, out var port)) return;
            string? remote = null;
            int? remotePort = null;
            if (pair.Length == 2 && TryAddress(pair[1], out var target, out var targetPort)) { remote = target; remotePort = targetPort; }
            var owner = processes.GetValueOrDefault(pid);
            ports.Add(new(address, port, state, new(owner is null ? ObservationStatus.Partial : ObservationStatus.Success, owner,
                owner is null ? "OWNER_UNVERIFIED" : null, DateTimeOffset.UtcNow), remote, remotePort));
        }
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            if (line[0] is 'p' or 'f') { Flush(); name = null; state = "UNKNOWN"; }
            if (line[0] == 'p') { _ = int.TryParse(line.AsSpan(1), out pid); }
            else if (line[0] == 'n') name = line[1..];
            else if (line.StartsWith("TST=", StringComparison.Ordinal)) state = line[4..];
        }
        Flush();
        return ports;
    }

    private static bool TryAddress(string text, out string address, out int port)
    {
        address = ""; port = 0;
        var colon = text.LastIndexOf(':');
        if (colon < 1 || !int.TryParse(text.AsSpan(colon + 1), out port) || port is < 1 or > 65535) return false;
        address = text[..colon].Trim('[', ']');
        return address == "*" || IPAddress.TryParse(address, out _);
    }
}
