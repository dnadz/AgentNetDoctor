using Microsoft.Win32;
using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Adapters;

public static class InstalledProductReader
{
    // Uninstall records are discovery candidates, not executable trust or installation-completeness proof.
    public static IReadOnlyList<InstalledProduct> Read(CancellationToken cancellationToken)
    {
        List<InstalledProduct> products = [];
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false);
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var entry = uninstall.OpenSubKey(name, false);
                    if (entry?.GetValue("DisplayName") is not string displayName) continue;
                    // Read metadata only; never evaluate uninstall strings or invoke candidate executables.
                    if (ToolCatalog.MatchInstalled(displayName, entry.GetValue("DisplayVersion") as string) is not null)
                        products.Add(new(displayName, entry.GetValue("DisplayVersion") as string, $"{hive}/{view}"));
                }
            }
        return products.Distinct().ToArray();
    }
}
