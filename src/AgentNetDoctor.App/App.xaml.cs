using AgentNetDoctor.Core.Launch;
using AgentNetDoctor.Windows.Launch;
using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace AgentNetDoctor.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var request = StartupRequest.Parse(e.Args);
        if (request.Mode == StartupMode.Interactive)
        {
            new MainWindow().Show();
            return;
        }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (request.Mode == StartupMode.Invalid) { Shutdown(2); return; }
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentNetDoctor", "launch-profiles");
            var profile = await new LaunchProfileStore(root).ReadAsync(request.ProfileId!, CancellationToken.None);
            // Real registrations remain locked until each version's proxy semantics and identity are verified.
            var result = await new TargetLauncher([]).StartAsync(profile, CancellationToken.None);
            Shutdown(result.State switch { LaunchState.StartedNeedsVerification => 0, LaunchState.NeedsUserExit => 4, _ => 3 });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or System.Security.SecurityException or ArgumentException)
        {
            Shutdown(5);
        }
    }
}
