using System.ComponentModel;
using System.Diagnostics;
using AgentNetDoctor.Core.Collection;
using AgentNetDoctor.Core.Models;
using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.Windows.Collection;

/// <summary>Read-only collection. No remote requests, command lines, configuration writes or elevation.</summary>
public sealed class WindowsSnapshotCollector : ISnapshotCollector
{
    private static Observation<T> Result<T>(ObservationStatus status, T? value, string? error = null) =>
        new(status, value, error, DateTimeOffset.UtcNow);

    public async Task<NetworkSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        var proxy = NetworkEnvironmentReader.ReadCurrentUserProxy();
        var ports = await ReadPorts(cancellationToken);
        var tools = ReadTools(cancellationToken);
        return new(Guid.NewGuid().ToString("N"), 1, DateTimeOffset.UtcNow, proxy, ports, tools,
            ObservationStatus.Partial, NetworkEnvironmentReader.Read(cancellationToken));
    }

    private static async Task<Observation<IReadOnlyList<PortRecord>>> ReadPorts(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "netstat.exe"), "-ano -p tcp")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
            };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                // Only our own read-only collector helper is terminated; never an Agent/VPN process.
                if (!process.HasExited) process.Kill();
                throw;
            }
            var output = await outputTask;
            await errorTask; // Drain only. Do not export raw helper output.
            if (process.ExitCode != 0) return Result<IReadOnlyList<PortRecord>>(ObservationStatus.Failed, null, "TCP_TABLE_READ_FAILED");
            var table = TcpTableParser.Parse(output);
            var owners = new Dictionary<int, Observation<ProcessIdentity>>();
            List<PortRecord> rows = [];
            foreach (var row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!owners.TryGetValue(row.Pid, out var owner)) owners[row.Pid] = owner = ReadIdentity(row.Pid);
                rows.Add(new(row.Address, row.Port, row.State, owner, row.RemoteAddress, row.RemotePort));
            }
            // Table and identities are sequential observations, not an atomic ownership proof.
            return Result<IReadOnlyList<PortRecord>>(ObservationStatus.Partial, rows,
                table.Complete ? "NON_ATOMIC_PROCESS_OBSERVATION" : "TCP_TABLE_PARTIAL");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Result<IReadOnlyList<PortRecord>>(ObservationStatus.Failed, null, "TCP_TABLE_TIMEOUT"); }
        catch (Win32Exception ex)
        { return Result<IReadOnlyList<PortRecord>>(ex.NativeErrorCode == 5 ? ObservationStatus.PermissionDenied : ObservationStatus.Failed, null, "TCP_TABLE_UNAVAILABLE"); }
    }

    private static Observation<ProcessIdentity> ReadIdentity(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var started = process.StartTime.ToUniversalTime();
            var path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return Result<ProcessIdentity>(ObservationStatus.Partial, null, "PROCESS_PATH_UNAVAILABLE");
            return Result(ObservationStatus.Success, new ProcessIdentity(pid, started, path));
        }
        catch (Win32Exception) { return Result<ProcessIdentity>(ObservationStatus.PermissionDenied, null, "PROCESS_ACCESS_DENIED"); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return Result<ProcessIdentity>(ObservationStatus.NotTested, null, "PROCESS_EXITED"); }
    }

    private static Observation<IReadOnlyList<ToolInstance>> ReadTools(CancellationToken cancellationToken)
    {
        List<ToolInstance> tools = [];
        bool partial = false;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name;
                try { name = process.ProcessName.ToLowerInvariant(); }
                catch (InvalidOperationException) { partial = true; continue; }
                var candidate = ToolCatalog.MatchProcess(name);
                if (candidate is null) continue;
                var identity = ReadIdentity(process.Id);
                partial |= identity.Status != ObservationStatus.Success;
                // Process names are candidates only, not authenticated product or installation identities.
                tools.Add(new(candidate.Id, "运行进程候选（尚未核实产品形态）", null, identity.Value));
            }
        }
        return Result<IReadOnlyList<ToolInstance>>(ObservationStatus.Partial, tools,
            partial ? "TOOL_IDENTITIES_INCOMPLETE" : "INSTALLED_TOOLS_NOT_ENUMERATED");
    }
}
