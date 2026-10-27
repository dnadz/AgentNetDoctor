using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace AgentNetDoctor.Core.Repair;

public sealed record ManualProxyConfiguration(int Flags, string Server, string Bypass, string AutoConfigUrl, bool ManagedOrUnknown);
public enum LocalEndpointState { Accepting, Refused, Unknown }
public interface IManualProxySettings
{
    ManualProxyConfiguration Read();
    bool CompareExchangeFlags(ManualProxyConfiguration expected, int flags);
}
public interface ILocalEndpointCheck
{
    Task<LocalEndpointState> CheckAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
}
public sealed record ManualProxyPlan(int SchemaVersion, string Id, DateTimeOffset ExpiresAt, ManualProxyConfiguration Before);
public sealed record ManualProxyRecord(int SchemaVersion, ManualProxyPlan Plan, string Stage);

public sealed class ManualProxyRepair(IManualProxySettings settings, ILocalEndpointCheck endpoints, IProtectedBackupStore backups)
{
    public static string Digest(ManualProxyPlan plan) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));
    public static IPEndPoint? EligibleEndpoint(ManualProxyConfiguration state)
    {
        if (state.ManagedOrUnknown || state.Flags is not (2 or 3) || !string.IsNullOrWhiteSpace(state.AutoConfigUrl) ||
            string.IsNullOrWhiteSpace(state.Server) || state.Server.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate("http://" + state.Server, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.Port is < 1 or > 65535 ||
            !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip)) return null;
        return new(ip, uri.Port);
    }
    public async Task<ManualProxyPlan?> PrepareAsync(CancellationToken cancellationToken)
    {
        var before = settings.Read();
        if (!await RefusedTwice(before, cancellationToken) || settings.Read() != before) return null;
        return new(1, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddMinutes(2), before);
    }
    public async Task<string> ApplyAsync(ManualProxyPlan plan, string approvedDigest, CancellationToken cancellationToken)
    {
        if (plan.SchemaVersion != 1 || !Guid.TryParseExact(plan.Id, "N", out _) || Digest(plan) != approvedDigest || plan.ExpiresAt < DateTimeOffset.UtcNow || settings.Read() != plan.Before ||
            !await RefusedTwice(plan.Before, cancellationToken)) throw new InvalidOperationException("计划过期、端口状态变化或设置已被修改，请重新检查。");
        // The durable intent is sufficient to inspect either state after interruption; never auto-replay it.
        var id = await Save(new(1, plan, "Prepared"), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!settings.CompareExchangeFlags(plan.Before, 1)) throw new InvalidOperationException("设置发生变化，未继续修复。");
        return id;
    }
    public async Task<ManualProxyRecord> ReadRecordAsync(string id, CancellationToken cancellationToken)
    {
        var bytes = await backups.ReadAsync(id, "user-manual-proxy", cancellationToken);
        try
        {
            var record = JsonSerializer.Deserialize<ManualProxyRecord>(bytes);
            if (record is null || record.SchemaVersion != 1 || record.Plan is null || record.Plan.SchemaVersion != 1 ||
                !Guid.TryParseExact(record.Plan.Id, "N", out _) || EligibleEndpoint(record.Plan.Before) is null)
                throw new InvalidDataException("恢复记录无效。");
            return record;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public async Task<bool> RollbackAsync(string id, CancellationToken cancellationToken)
    {
        var record = await ReadRecordAsync(id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var expected = record.Plan.Before with { Flags = 1 };
        return settings.CompareExchangeFlags(expected, record.Plan.Before.Flags);
    }
    public string Inspect(ManualProxyRecord record)
    {
        var current = settings.Read();
        return current == record.Plan.Before ? "当前匹配修复前状态" :
            current == record.Plan.Before with { Flags = 1 } ? "当前匹配修复后状态，可选择回滚；业务连接待验证" :
            "与修复前后均不匹配，禁止覆盖；可能有外部改动";
    }
    private async Task<bool> RefusedTwice(ManualProxyConfiguration state, CancellationToken cancellationToken)
    {
        var endpoint = EligibleEndpoint(state);
        if (endpoint is null || await endpoints.CheckAsync(endpoint, cancellationToken) != LocalEndpointState.Refused) return false;
        await Task.Delay(150, cancellationToken);
        return await endpoints.CheckAsync(endpoint, cancellationToken) == LocalEndpointState.Refused;
    }
    private async Task<string> Save(ManualProxyRecord record, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        try { return await backups.SaveAsync("user-manual-proxy", bytes, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
