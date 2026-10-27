using AgentNetDoctor.Core.Models;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;

namespace AgentNetDoctor.Core.Adapters;

public sealed record ConfigurationInspection(ObservationStatus Status, string? ErrorCode,
    IReadOnlyDictionary<string, string> ProxyFields, bool ProviderConfigured, bool HasProxyCredentials);

public static class ConfigurationInspector
{
    private static readonly string[] ProxyVariables = ["https_proxy", "HTTPS_PROXY", "http_proxy", "HTTP_PROXY", "all_proxy", "ALL_PROXY", "no_proxy", "NO_PROXY"];

    public static ConfigurationInspection ReadJson(string text, string product)
    {
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('\uFEFF'), new JsonDocumentOptions
            { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 64 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicates(root)) return Invalid();
            Dictionary<string, string> fields = [];
            bool credentials = false, provider = false;
            if (product == "claude-code" && root.TryGetProperty("env", out var environment))
            {
                if (environment.ValueKind != JsonValueKind.Object) return Invalid();
                foreach (var variable in ProxyVariables)
                    if (environment.TryGetProperty(variable, out var value)) Add(variable, value);
                provider = environment.TryGetProperty("ANTHROPIC_BASE_URL", out _) || environment.TryGetProperty("ANTHROPIC_API_KEY", out _) || environment.TryGetProperty("ANTHROPIC_AUTH_TOKEN", out _);
            }
            else if (product is "antigravity" or "vscode")
            {
                if (root.TryGetProperty("http.proxy", out var proxy)) Add("http.proxy", proxy);
                foreach (var key in new[] { "http.proxySupport", "http.noProxy", "http.proxyStrictSSL" })
                    if (root.TryGetProperty(key, out _)) fields[key] = "已配置（值未导出）";
            }
            else if (product != "claude-code")
                return new(ObservationStatus.UnsupportedVersion, "CONFIG_SCHEMA_NOT_VERIFIED", fields, false, false);
            return new(ObservationStatus.Success, null, fields, provider, credentials);

            void Add(string key, JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.String) throw new JsonException();
                var value = element.GetString()!;
                if (key.Equals("NO_PROXY", StringComparison.OrdinalIgnoreCase)) fields[key] = "已配置（值未导出）";
                else if (value.Length == 0) fields[key] = "空值（不证明绕过TUN）";
                else if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Host.Length > 0 && uri.Scheme is "http" or "https" or "socks5" or "socks5h")
                {
                    credentials |= uri.UserInfo.Length != 0;
                    fields[key] = uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.AbsolutePath != "/" || uri.Fragment.Length != 0
                        ? "含凭据或额外内容（地址未导出）" : uri.AbsoluteUri;
                }
                else fields[key] = "地址格式未知（原值未导出）";
            }
        }
        catch (JsonException) { return Invalid(); }
    }

    public static ConfigurationInspection ReadCodexToml(string text)
    {
        try
        {
            // Untyped deserialization alone accepts repeated keys; validate semantic syntax first.
            SyntaxParser.ParseStrict(text.TrimStart('\uFEFF'), validate: true);
            var table = TomlSerializer.Deserialize<TomlTable>(text.TrimStart('\uFEFF'), new TomlSerializerOptions
            { DuplicateKeyHandling = TomlDuplicateKeyHandling.Error, MaxDepth = 64 });
            return new(ObservationStatus.Success, null, new Dictionary<string, string>(),
                table?.ContainsKey("model_provider") == true || table?.ContainsKey("model_providers") == true, false);
        }
        catch (TomlException) { return Invalid(); }
    }

    private static ConfigurationInspection Invalid() => new(ObservationStatus.Failed, "CONFIG_SYNTAX_INVALID", new Dictionary<string, string>(), false, false);

    private static bool HasDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicates(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) if (HasDuplicates(item)) return true;
        return false;
    }
}
