using AgentNetDoctor.Core.Collection;

namespace AgentNetDoctor.UnitTests;

public sealed class SnapshotCollectorTests
{
    [Fact]
    public void ParsesIpv4AndIpv6WithoutTreatingRemote443AsListenerConflict()
    {
        var table = TcpTableParser.Parse("  TCP  127.0.0.1:1234  0.0.0.0:0  LISTENING  42\n  TCP  [::1]:5678  [::]:0  LISTENING  43\n  TCP  192.0.2.1:60000  192.0.2.2:443  ESTABLISHED  44");
        Assert.True(table.Complete);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("::1", table.Rows[1].Address);
        Assert.Equal(60000, table.Rows[2].Port);
        Assert.Equal("192.0.2.2", table.Rows[2].RemoteAddress);
        Assert.Equal(443, table.Rows[2].RemotePort);
        Assert.Equal("::", table.Rows[1].RemoteAddress);
    }

    [Fact]
    public void MalformedTcpRowIsPartialRatherThanHealthyEmptyTable()
    {
        Assert.False(TcpTableParser.Parse(" TCP  broken").Complete);
    }
}
