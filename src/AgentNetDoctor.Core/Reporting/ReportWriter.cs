using AgentNetDoctor.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentNetDoctor.Core.Reporting;

public sealed class ReportWriter
{
    public async Task WriteAsync(ReportData data, Stream destination, CancellationToken cancellationToken)
    {
        var safe = new
        {
            SchemaVersion = 1,
            Id = Redactor.Redact(data.Id),
            Evidence = data.Evidence.Select(item => new Evidence(
                Redactor.Redact(item.Id), item.Status,
                item.Value is null ? null : Redactor.Redact(item.Value),
                item.ErrorCode is null ? null : Redactor.Redact(item.ErrorCode)))
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(safe, options);
        await destination.WriteAsync(bytes, cancellationToken);
    }
}
