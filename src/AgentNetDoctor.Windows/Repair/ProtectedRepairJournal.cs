using AgentNetDoctor.Core.Repair;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentNetDoctor.Windows.Repair;

public sealed class ProtectedRepairJournal(string root) : IRepairJournal
{
    private const string Role = "repair-journal";
    private sealed record Envelope(int Version, string Digest, RepairJournalEntry Entry);
    private readonly ProtectedBackupStore storage = new(root);

    public async Task SaveAsync(RepairJournalEntry entry, CancellationToken cancellationToken)
    {
        Validate(entry);
        entry = entry with { Plan = entry.Plan with { Changes = Array.AsReadOnly(entry.Plan.Changes.ToArray()) } };
        var directory = storage.PrivateDirectory;
        var lockPath = LocalFileGuard.Validate(Path.Combine(directory, entry.Id + ".lock"));
        await using var gate = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new Envelope(1, RepairPlanConfirmation.Digest(entry.Plan), entry));
        string blobId;
        try { blobId = await storage.SaveAsync(Role, serialized, cancellationToken); }
        finally { CryptographicOperations.ZeroMemory(serialized); }
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pending");
        await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await file.WriteAsync(Encoding.ASCII.GetBytes(blobId), cancellationToken);
            await file.FlushAsync(cancellationToken);
            file.Flush(true);
        }
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporary, LocalFileGuard.Validate(Path.Combine(directory, entry.Id + ".head")), true);
    }

    public async Task<RepairJournalEntry> ReadAsync(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("记录ID无效", nameof(id));
        var head = LocalFileGuard.Validate(Path.Combine(storage.PrivateDirectory, id + ".head"));
        var pointer = new byte[32];
        await using (var file = new FileStream(head, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous))
        {
            if (file.Length != pointer.Length) throw new InvalidDataException("记录索引无效");
            await file.ReadExactlyAsync(pointer, cancellationToken);
        }
        var blobId = Encoding.ASCII.GetString(pointer);
        if (!Guid.TryParseExact(blobId, "N", out _)) throw new InvalidDataException("记录索引无效");
        var bytes = await storage.ReadAsync(blobId, Role, cancellationToken);
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes) ?? throw new InvalidDataException("记录无效");
            if (envelope.Version != 1 || envelope.Entry is null) throw new InvalidDataException("记录版本无效");
            Validate(envelope.Entry);
            if (envelope.Entry.Id != id || envelope.Digest != RepairPlanConfirmation.Digest(envelope.Entry.Plan))
                throw new InvalidDataException("记录身份或摘要不匹配");
            return envelope.Entry with { Plan = envelope.Entry.Plan with { Changes = Array.AsReadOnly(envelope.Entry.Plan.Changes.ToArray()) } };
        }
        catch (JsonException) { throw new InvalidDataException("记录结构无效"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> ids = Directory.EnumerateFiles(storage.PrivateDirectory, "*.head", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().Where(id => Guid.TryParseExact(id, "N", out _)).ToArray();
        return Task.FromResult(ids);
    }

    private static void Validate(RepairJournalEntry entry)
    {
        if (entry.Plan is null || !Guid.TryParseExact(entry.Id, "N", out _) || entry.Id != entry.Plan.Id ||
            !Guid.TryParseExact(entry.BackupId, "N", out _) || entry.Plan.Changes is null || entry.Plan.Changes.Count is < 1 or > 16 ||
            !Enum.IsDefined(entry.Direction) || !Enum.IsDefined(entry.Stage) || !Hash(entry.SourceFingerprint) || !Hash(entry.ExpectedFingerprint))
            throw new InvalidDataException("记录字段无效");
        static bool Hash(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);
    }
}
