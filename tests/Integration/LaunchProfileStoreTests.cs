using AgentNetDoctor.Core.Launch;
using AgentNetDoctor.Windows.Launch;
using System.Security.Cryptography;

namespace AgentNetDoctor.IntegrationTests;

public sealed class LaunchProfileStoreTests
{
    [Fact]
    public async Task ConfirmedProfileReloadsButCannotRegisterExecutableOrStoreCredentials()
    {
        var root = Path.Combine(new RepairFixture().Root, "profiles");
        var store = new LaunchProfileStore(root);
        var profile = new LaunchProfile("synthetic-child", "1", new string('A', 64), LaunchNetworkMode.ExplicitHttpProxy, "http://127.0.0.1:9090");
        var approval = LaunchApproval.FromReviewedProfile(profile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(profile with { Version = "2" }, approval, CancellationToken.None));
        var secret = profile with { Proxy = "http://user:password@127.0.0.1:9090" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(secret, LaunchApproval.FromReviewedProfile(secret), CancellationToken.None));
        var id = await store.SaveAsync(profile, approval, CancellationToken.None);
        var reloaded = await new LaunchProfileStore(root).ReadAsync(id, CancellationToken.None);
        Assert.Equal(profile, reloaded);
        Assert.Equal(LaunchState.Rejected, (await new TargetLauncher([]).StartAsync(reloaded, CancellationToken.None)).State);
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync("../outside", CancellationToken.None));
        var file = Path.Combine(root, id + ".bin");
        var bytes = await File.ReadAllBytesAsync(file);
        bytes[bytes.Length / 2] ^= 0xFF;
        await File.WriteAllBytesAsync(file, bytes);
        await Assert.ThrowsAsync<CryptographicException>(() => store.ReadAsync(id, CancellationToken.None));
    }
}
