using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;
using System.Security;
using System.Text;

namespace AgentNetDoctor.Adapters;

public static class ConfigurationFileReader
{
    public static async Task<ConfigurationSource> ReadAsync(string productId, string scope, string path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!WindowsPathPolicy.IsLocalDriveSyntax(path)) return Result(ObservationStatus.NotTested, "CONFIG_REMOTE_OR_DEVICE_PATH_BLOCKED");
            path = Path.GetFullPath(path);
            var rootPath = Path.GetPathRoot(path)!;
            if (new DriveInfo(rootPath).DriveType != DriveType.Fixed) return Result(ObservationStatus.NotTested, "CONFIG_NONLOCAL_DRIVE_BLOCKED");
            // Walk root first: checking a leaf below a junction could itself access a remote target.
            var prefix = rootPath;
            foreach (var part in path[rootPath.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                prefix = Path.Combine(prefix, part);
                FileAttributes attributes;
                try { attributes = File.GetAttributes(prefix); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { break; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) return Result(ObservationStatus.NotTested, "CONFIG_REPARSE_PATH_BLOCKED");
            }
            const int limit = 1024 * 1024;
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (file.Length > limit) return Result(ObservationStatus.Partial, "CONFIG_TOO_LARGE");
            var bytes = new byte[limit + 1];
            int total = 0, count;
            while (total < bytes.Length && (count = await file.ReadAsync(bytes.AsMemory(total), cancellationToken)) != 0) total += count;
            if (total > limit) return Result(ObservationStatus.Partial, "CONFIG_TOO_LARGE");
            var text = new UTF8Encoding(false, true).GetString(bytes, 0, total);
            var inspection = productId == "codex" ? ConfigurationInspector.ReadCodexToml(text) : ConfigurationInspector.ReadJson(text, productId);
            return new(scope, inspection.Status, inspection.ErrorCode, inspection);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        { return Result(ObservationStatus.NotApplicable, "CONFIG_NOT_PRESENT"); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        { return Result(ObservationStatus.PermissionDenied, "CONFIG_ACCESS_DENIED"); }
        catch (DecoderFallbackException) { return Result(ObservationStatus.Failed, "CONFIG_ENCODING_INVALID"); }
        catch (IOException) { return Result(ObservationStatus.Failed, "CONFIG_READ_FAILED"); }

        ConfigurationSource Result(ObservationStatus status, string error) => new(scope, status, error, null);
    }
}
