namespace AgentNetDoctor.Core.Launch;

public enum StartupMode { Interactive, Launch, Invalid }
public sealed record StartupRequest(StartupMode Mode, string? ProfileId = null)
{
    public static StartupRequest Parse(IReadOnlyList<string> arguments) => arguments.Count == 0 ? new(StartupMode.Interactive) :
        arguments.Count == 2 && arguments[0] == "--launch" && Guid.TryParseExact(arguments[1], "N", out _) ?
            new(StartupMode.Launch, arguments[1]) : new(StartupMode.Invalid);
}
