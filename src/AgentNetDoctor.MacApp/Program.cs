using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Mac;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace AgentNetDoctor.MacApp;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-check" })) return SelfCheckAsync().GetAwaiter().GetResult();
        if (args.Length != 0) return 2;
        return BuildApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildApp() => AppBuilder.Configure<DoctorApplication>().UsePlatformDetect();
    private static async Task<int> SelfCheckAsync()
    {
        if (!OperatingSystem.IsMacOS()) { Console.Error.WriteLine("Mac native collection requires macOS."); return 2; }
        var inspection = await new MacSnapshotCollector().InspectAsync(default);
        Console.WriteLine($"catalog={ToolCatalog.All.Count}; proxy={inspection.Snapshot.UserProxy.Status}; tools={inspection.Snapshot.Tools.Status}; tcp={inspection.Snapshot.Ports.Status}");
        return inspection.Snapshot.UserProxy.Value is null || inspection.Snapshot.Tools.Value is null || inspection.Snapshot.Ports.Value is null ? 1 : 0;
    }
}

public sealed class DoctorApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime) lifetime.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
