using System.Net;
using AgentNetDoctor.Core.Repair;

namespace AgentNetDoctor.UnitTests;

public sealed class ManualProxyRepairTests
{
    [Theory]
    [InlineData(3, "127.0.0.1:7890", "", false, true)]
    [InlineData(3, "[::1]:7890", "", false, true)]
    [InlineData(11, "127.0.0.1:7890", "", false, false)]
    [InlineData(3, "127.0.0.1:7890", "http://example.test/pac", false, false)]
    [InlineData(3, "127.0.0.1:7890", "", true, false)]
    [InlineData(3, "http=127.0.0.1:7890;https=127.0.0.1:7890", "", false, false)]
    [InlineData(3, "10.0.0.1:7890", "", false, false)]
    [InlineData(3, "user:password@127.0.0.1:7890", "", false, false)]
    public void OnlyUnmanagedStaticLoopbackIsEligible(int flags, string server, string pac, bool managed, bool eligible) =>
        Assert.Equal(eligible, ManualProxyRepair.EligibleEndpoint(new(flags, server, "", pac, managed)) is not null);

    [Fact]
    public async Task RequiresRepeatedRefusalAndConfirmationAndPreservesExternalEdits()
    {
        var settings = new Settings(); var endpoint = new Endpoint(); var backup = new Backup();
        var repair = new ManualProxyRepair(settings, endpoint, backup);
        endpoint.State = LocalEndpointState.Unknown;
        Assert.Null(await repair.PrepareAsync(CancellationToken.None));
        endpoint.State = LocalEndpointState.Accepting;
        Assert.Null(await repair.PrepareAsync(CancellationToken.None));
        endpoint.State = LocalEndpointState.Refused;
        var plan = (await repair.PrepareAsync(CancellationToken.None))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => repair.ApplyAsync(plan, "wrong", CancellationToken.None));
        var id = await repair.ApplyAsync(plan, ManualProxyRepair.Digest(plan), CancellationToken.None);
        Assert.Equal(1, settings.State.Flags);
        settings.State = settings.State with { Bypass = "external" };
        Assert.False(await repair.RollbackAsync(id, CancellationToken.None));
        settings.State = plan.Before with { Flags = 1 };
        Assert.True(await repair.RollbackAsync(id, CancellationToken.None));
        Assert.Equal(plan.Before, settings.State);
    }
    private sealed class Settings : IManualProxySettings
    {
        public ManualProxyConfiguration State = new(3, "127.0.0.1:7890", "<local>", "", false);
        public ManualProxyConfiguration Read() => State;
        public bool CompareExchangeFlags(ManualProxyConfiguration expected, int flags)
        { if (State != expected) return false; State = State with { Flags = flags }; return true; }
    }
    [Fact]
    public async Task ExpiryAndBackupFailureCannotChangeSettings()
    {
        var settings = new Settings(); var endpoint = new Endpoint { State = LocalEndpointState.Refused };
        var repair = new ManualProxyRepair(settings, endpoint, new FailingBackup());
        var plan = (await repair.PrepareAsync(CancellationToken.None))!;
        var expired = plan with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => repair.ApplyAsync(expired, ManualProxyRepair.Digest(expired), CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(() => repair.ApplyAsync(plan, ManualProxyRepair.Digest(plan), CancellationToken.None));
        Assert.Equal(plan.Before, settings.State);
    }
    private sealed class FailingBackup : IProtectedBackupStore
    {
        public Task<string> SaveAsync(string targetId, byte[] original, CancellationToken cancellationToken) => throw new IOException("synthetic backup failure");
        public Task<byte[]> ReadAsync(string backupId, string targetId, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
    private sealed class Endpoint : ILocalEndpointCheck
    {
        public LocalEndpointState State;
        public Task<LocalEndpointState> CheckAsync(IPEndPoint endpoint, CancellationToken cancellationToken) => Task.FromResult(State);
    }
    private sealed class Backup : IProtectedBackupStore
    {
        private byte[] bytes = [];
        public Task<string> SaveAsync(string targetId, byte[] original, CancellationToken cancellationToken) { bytes = original.ToArray(); return Task.FromResult("fixture"); }
        public Task<byte[]> ReadAsync(string backupId, string targetId, CancellationToken cancellationToken) => Task.FromResult(bytes.ToArray());
    }
}
