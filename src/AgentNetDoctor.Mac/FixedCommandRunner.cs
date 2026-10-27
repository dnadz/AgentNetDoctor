using System.Diagnostics;
using System.Text;

namespace AgentNetDoctor.Mac;

public sealed record CommandResult(int ExitCode, string Output);
public interface IMacCommandRunner
{
    Task<CommandResult> RunAsync(string command, CancellationToken cancellationToken);
}

public sealed class FixedCommandRunner : IMacCommandRunner
{
    public async Task<CommandResult> RunAsync(string command, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("仅在macOS执行采集");
        var (file, arguments) = command switch
        {
            "proxy" => ("/usr/sbin/scutil", new[] { "--proxy" }),
            "processes" => ("/bin/ps", new[] { "-ww", "-axo", "pid=,lstart=,comm=" }),
            "ports" => ("/usr/sbin/lsof", new[] { "-nP", "-iTCP", "-FpcfnT" }),
            _ => throw new ArgumentException("非白名单采集命令", nameof(command))
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var start = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["LC_ALL"] = "C";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("COMMAND_START_FAILED");
        try
        {
            var stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var stderr = ReadBoundedAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            return new(process.ExitCode, await stdout);
        }
        finally
        {
            if (!process.HasExited) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        const int limit = 1_048_576;
        StringBuilder output = new();
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (output.Length + read > limit) throw new IOException("COMMAND_OUTPUT_LIMIT");
            output.Append(buffer, 0, read);
        }
        return output.ToString();
    }
}
