using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Windows.Repair;

internal static class LocalFileGuard
{
    public static string Validate(string path)
    {
        if (!WindowsPathPolicy.IsLocalDriveSyntax(path)) throw new IOException("仅允许本地固定盘路径");
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        if (new DriveInfo(root).DriveType != DriveType.Fixed) throw new IOException("仅允许本地固定盘路径");
        var prefix = root;
        foreach (var part in full[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            prefix = Path.Combine(prefix, part);
            try { if ((File.GetAttributes(prefix) & FileAttributes.ReparsePoint) != 0) throw new IOException("不允许重解析路径"); }
            catch (FileNotFoundException) { break; }
            catch (DirectoryNotFoundException) { break; }
        }
        return full;
    }
}
