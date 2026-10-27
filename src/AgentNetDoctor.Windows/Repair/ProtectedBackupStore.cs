using AgentNetDoctor.Core.Repair;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Windows.Repair;

public sealed class ProtectedBackupStore(string root) : IProtectedBackupStore
{
    internal string PrivateDirectory { get { EnsureRoot(); return Path.GetFullPath(root); } }
    public string PrivateDirectoryPath() => PrivateDirectory;
    private sealed record Payload(int Version, string Id, string TargetId, byte[] Data, string Hash);
    public async Task<string> SaveAsync(string targetId, byte[] original, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (original.Length > 1024 * 1024) throw new InvalidDataException("备份超出大小限制");
        EnsureRoot();
        var id = Guid.NewGuid().ToString("N");
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Payload(1, id, targetId, original, Convert.ToHexString(SHA256.HashData(original))));
        try
        {
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            await using var file = new FileStream(Resolve(id), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await file.WriteAsync(encrypted, cancellationToken);
            await file.FlushAsync(cancellationToken);
            file.Flush(true);
            return id;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public async Task<byte[]> ReadAsync(string backupId, string targetId, CancellationToken cancellationToken)
    {
        var path = Resolve(backupId);
        EnsureRoot();
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("备份不能是重解析点");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (file.Length is <= 0 or > 2 * 1024 * 1024) throw new InvalidDataException("备份大小无效");
        var encrypted = new byte[(int)file.Length];
        await file.ReadExactlyAsync(encrypted, cancellationToken);
        var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(plain) ?? throw new InvalidDataException("备份结构无效");
            if (payload.Version != 1 || payload.Id != backupId || payload.TargetId != targetId ||
                payload.Data is null || payload.Data.Length > 1024 * 1024 || Convert.ToHexString(SHA256.HashData(payload.Data)) != payload.Hash)
            {
                if (payload.Data is not null) CryptographicOperations.ZeroMemory(payload.Data);
                throw new InvalidDataException("备份身份或完整性校验失败");
            }
            return payload.Data;
        }
        catch (JsonException) { throw new InvalidDataException("备份结构无效"); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private string Resolve(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("备份ID无效", nameof(id));
        return Path.Combine(Path.GetFullPath(root), id + ".bin");
    }

    private void EnsureRoot()
    {
        if (!WindowsPathPolicy.IsLocalDriveSyntax(root)) throw new IOException("备份只允许本地固定磁盘路径");
        var directory = new DirectoryInfo(Path.GetFullPath(root));
        if (new DriveInfo(directory.Root.FullName).DriveType != DriveType.Fixed) throw new IOException("备份只允许本地固定磁盘路径");
        Stack<DirectoryInfo> ancestors = [];
        for (DirectoryInfo? parent = directory; parent is not null; parent = parent.Parent)
            ancestors.Push(parent);
        foreach (var parent in ancestors)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("备份路径不能包含重解析点");
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new UnauthorizedAccessException("当前用户身份不可用");
        if (!directory.Exists)
        {
            var security = new DirectorySecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.Create(security);
        }
        var actual = directory.GetAccessControl();
        if (!actual.AreAccessRulesProtected || !user.Equals(actual.GetOwner(typeof(SecurityIdentifier))))
            throw new UnauthorizedAccessException("备份目录保护状态不满足要求");
        foreach (FileSystemAccessRule rule in actual.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !user.Equals(rule.IdentityReference))
                throw new UnauthorizedAccessException("备份目录允许其他身份访问");
    }
}
