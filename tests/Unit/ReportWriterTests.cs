using System.Text;
using System.Text.Json;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Reporting;

namespace AgentNetDoctor.UnitTests;

public class ReportWriterTests
{
    [Theory]
    [InlineData(ObservationStatus.NotTested)]
    [InlineData(ObservationStatus.PermissionDenied)]
    public async Task PreservesIncompleteStatus(ObservationStatus status)
    {
        using var stream = new MemoryStream();
        var data = new ReportData("test", [new Evidence("proxy", status, null, null)]);
        await new ReportWriter().WriteAsync(data, stream, default);
        using var json = JsonDocument.Parse(stream.ToArray());
        Assert.Equal(status.ToString(), json.RootElement.GetProperty("evidence")[0].GetProperty("status").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task RedactsCredentialAndPersonalPathAtExportBoundary()
    {
        using var stream = new MemoryStream();
        var message = "HTTPS_PROXY=http://user:synthetic-password@localhost:9000 API_KEY=synthetic-sensitive-value C:\\Users\\ExampleUser\\project";
        await new ReportWriter().WriteAsync(new ReportData("test", [new Evidence("proxy", ObservationStatus.Failed, message, "AUTH_ERROR")]), stream, default);
        var output = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("synthetic-password", output);
        Assert.DoesNotContain("synthetic-sensitive-value", output);
        Assert.DoesNotContain("ExampleUser", output);
        Assert.Contains("AUTH_ERROR", output);
    }

    [Fact]
    public async Task PropagatesWriteFailure()
    {
        using var stream = new MemoryStream();
        stream.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => new ReportWriter().WriteAsync(new ReportData("test", []), stream, default));
    }
}
