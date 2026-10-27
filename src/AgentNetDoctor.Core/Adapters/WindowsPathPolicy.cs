namespace AgentNetDoctor.Core.Adapters;

public static class WindowsPathPolicy
{
    public static bool IsLocalDriveSyntax(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/' &&
        !path[2..].Any(c => char.IsControl(c) || c is ':' or '?' or '*' or '"' or '<' or '>' or '|');
}
