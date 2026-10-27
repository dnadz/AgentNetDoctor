using System.Text.RegularExpressions;

namespace AgentNetDoctor.Core.Reporting;

public static class Redactor
{
    public static string Redact(string text)
    {
        text = Replace(text, @"(?i)(https?|socks5h?)://[^/\s@]+@", "$1://[redacted]@");
        text = Replace(text, """(?i)\b(api[_-]?key|access[_-]?token|authorization|password)["']?\s*[:=]\s*["']?(?:Bearer\s+)?[^\s,;"'}]+""", "$1=[redacted]");
        text = Replace(text, """(?i)\bBearer\s+[^\s,;"'}]+""", "Bearer [redacted]");
        text = Replace(text, @"(?i)([a-z]:\\Users\\)[^\\\s]+", "$1[user]");
        text = Replace(text, @"(/(?:Users|home)/)[^/\s]+", "$1[user]");
        return text;
    }

    private static string Replace(string text, string pattern, string replacement) =>
        Regex.Replace(text, pattern, replacement, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
}
