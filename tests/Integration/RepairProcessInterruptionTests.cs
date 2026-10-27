using AgentNetDoctor.Core.Repair;
using AgentNetDoctor.Windows.Repair;
using System.Diagnostics;

namespace AgentNetDoctor.IntegrationTests;

public sealed class RepairProcessInterruptionTests
{
    [Theory]
    [InlineData("before", 73, RecoveryOutcome.SourceStateObserved)]
    [InlineData("after", 74, RecoveryOutcome.ExpectedStateObserved)]
    public async Task AbruptChildExitLeavesReloadableEvidence(string point, int exitCode, RecoveryOutcome expected)
    {
        var fixture = new RepairFixture();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "owned-crash-fixture"), "synthetic test only");
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "crash-host", "RepairCrashHost.dll"));
        start.ArgumentList.Add(fixture.Root);
        start.ArgumentList.Add(point);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Test child did not start");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!child.HasExited) child.Kill();
            throw;
        }
        Assert.Equal(exitCode, child.ExitCode);
        var journal = new ProtectedRepairJournal(fixture.JournalRoot);
        var id = Assert.Single(await journal.ListAsync(CancellationToken.None));
        var entry = await journal.ReadAsync(id, CancellationToken.None);
        Assert.Equal(JournalStage.Prepared, entry.Stage);
        Assert.Equal(expected, (await RecoveryInspector.InspectAsync(entry, fixture.Store, CancellationToken.None)).Outcome);
        Assert.Equal(point == "before" ? fixture.Original : fixture.Expected, await File.ReadAllBytesAsync(fixture.Configuration));
        if (point == "after")
        {
            var record = new RepairRecord(entry.Plan, RepairState.AppliedNeedsVerification, entry.BackupId, entry.ExpectedFingerprint);
            Assert.Equal(RepairState.RolledBackNeedsVerification, await fixture.Engine.RollbackAsync(record, CancellationToken.None));
            Assert.Equal(fixture.Original, await File.ReadAllBytesAsync(fixture.Configuration));
        }
    }
}
