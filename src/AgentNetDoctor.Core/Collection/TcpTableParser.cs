namespace AgentNetDoctor.Core.Collection;

public sealed record TcpRow(string Address, int Port, string State, int Pid, string? RemoteAddress = null, int? RemotePort = null);
public sealed record TcpTable(IReadOnlyList<TcpRow> Rows, bool Complete);

public static class TcpTableParser
{
    public static TcpTable Parse(string text)
    {
        List<TcpRow> rows = [];
        bool complete = true;
        foreach (var line in text.Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || !fields[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields.Length != 5 || !int.TryParse(fields[4], out var pid)) { complete = false; continue; }
            var separator = fields[1].LastIndexOf(':');
            if (separator < 0 || !int.TryParse(fields[1][(separator + 1)..], out var port) || port is < 0 or > 65535)
            { complete = false; continue; }
            var remoteSeparator = fields[2].LastIndexOf(':');
            if (remoteSeparator < 0 || !int.TryParse(fields[2][(remoteSeparator + 1)..], out var remotePort) || remotePort is < 0 or > 65535)
            { complete = false; continue; }
            rows.Add(new(fields[1][..separator].Trim('[', ']'), port, fields[3], pid, fields[2][..remoteSeparator].Trim('[', ']'), remotePort));
        }
        return new(rows, complete);
    }
}
