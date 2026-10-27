using AgentNetDoctor.Core.Launch;
using AgentNetDoctor.Windows.Repair;
using System.Security.Cryptography;
using System.Text.Json;

namespace AgentNetDoctor.Windows.Launch;

public sealed class LaunchProfileStore(string root)
{
    private const string Role = "launch-profile";
    private sealed record Envelope(int Version, LaunchProfile Profile);
    private readonly ProtectedBackupStore storage = new(root);

    public async Task<string> SaveAsync(LaunchProfile profile, LaunchApproval approval, CancellationToken cancellationToken)
    {
        if (!TargetLauncher.ValidProfile(profile) || !approval.Matches(profile)) throw new InvalidOperationException("启动配置无效或与已确认内容不符。");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, profile));
        try { return await storage.SaveAsync(Role, bytes, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public async Task<LaunchProfile> ReadAsync(string id, CancellationToken cancellationToken)
    {
        var bytes = await storage.ReadAsync(id, Role, cancellationToken);
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes);
            if (envelope is null || envelope.Version != 1 || envelope.Profile is null || !TargetLauncher.ValidProfile(envelope.Profile))
                throw new InvalidDataException("启动配置内容无效。");
            return envelope.Profile;
        }
        catch (JsonException) { throw new InvalidDataException("启动配置结构无效。"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
