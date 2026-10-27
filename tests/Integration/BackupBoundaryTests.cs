using System.Text;
using AgentNetDoctor.Windows.Repair;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;

namespace AgentNetDoctor.IntegrationTests;

public sealed class BackupBoundaryTests
{
    [Fact]
    public async Task CurrentUserRoundTripIsEncryptedAndBoundToTarget()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "synthetic-backup-" + Guid.NewGuid().ToString("N"));
        var store = new ProtectedBackupStore(root);
        var secret = Encoding.UTF8.GetBytes("synthetic-private-configuration");
        var id = await store.SaveAsync("fixture", secret, CancellationToken.None);
        try
        {
            Assert.DoesNotContain("synthetic-private-configuration", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(root, id + ".bin"))));
            Assert.Equal(secret, await store.ReadAsync(id, "fixture", CancellationToken.None));
            using var identity = WindowsIdentity.GetCurrent();
            var acl = new DirectoryInfo(root).GetAccessControl();
            Assert.True(acl.AreAccessRulesProtected);
            Assert.All(acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
                .Where(rule => rule.AccessControlType == AccessControlType.Allow), rule => Assert.Equal(identity.User, rule.IdentityReference));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(id, "other-target", CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync("../outside", "fixture", CancellationToken.None));
            await File.WriteAllBytesAsync(Path.Combine(root, id + ".bin"), new byte[128]);
            await Assert.ThrowsAsync<CryptographicException>(() => store.ReadAsync(id, "fixture", CancellationToken.None));
        }
        finally { File.Delete(Path.Combine(root, id + ".bin")); Directory.Delete(root); }
    }
}
