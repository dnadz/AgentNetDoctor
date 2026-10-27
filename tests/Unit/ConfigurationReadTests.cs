using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.UnitTests;

public sealed class ConfigurationReadTests
{
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"http.proxy\":\"http://127.0.0.1:1111\",\"http.proxy\":\"http://127.0.0.1:2222\"}")]
    public void InvalidOrAmbiguousJsonIsNotUnconfigured(string text)
    {
        Assert.Equal(ObservationStatus.Failed, ConfigurationInspector.ReadJson(text, "antigravity").Status);
    }

    [Fact]
    public void JsoncBomAndSecretsAreHandledAtReadBoundary()
    {
        var read = ConfigurationInspector.ReadJson("\uFEFF{ // synthetic fixture\n \"http.proxy\": \"http://user:synthetic-password@127.0.0.1:8080\", \"api_key\": \"synthetic-secret-value\", }", "antigravity");
        Assert.Equal(ObservationStatus.Success, read.Status);
        Assert.True(read.HasProxyCredentials);
        Assert.DoesNotContain("synthetic-password", string.Join(' ', read.ProxyFields.Values));
        Assert.DoesNotContain("synthetic-secret-value", string.Join(' ', read.ProxyFields.Values));
    }

    [Fact]
    public void ClaudeSettingsExposeOnlyProxyEnvNotProviderKeys()
    {
        var read = ConfigurationInspector.ReadJson("{\"env\":{\"HTTPS_PROXY\":\"http://127.0.0.1:8080\",\"ANTHROPIC_API_KEY\":\"synthetic-secret-value\"}}", "claude-code");
        Assert.Equal("http://127.0.0.1:8080/", read.ProxyFields["HTTPS_PROXY"]);
        Assert.Single(read.ProxyFields);
        Assert.True(read.ProviderConfigured);
    }

    [Fact]
    public void CodexSandboxProxyIsNotClientProxy()
    {
        var read = ConfigurationInspector.ReadCodexToml("# synthetic\nmodel_provider = 'synthetic'\n[permissions.workspace.network]\nproxy_url = 'http://127.0.0.1:3128'\n");
        Assert.Equal(ObservationStatus.Success, read.Status);
        Assert.True(read.ProviderConfigured);
        Assert.Empty(read.ProxyFields);
    }

    [Theory]
    [InlineData("model_provider = [broken")]
    [InlineData("model_provider = 'a'\nmodel_provider = 'b'")]
    public void InvalidTomlIsFailureWithoutRawDiagnosticLeak(string text)
    {
        var read = ConfigurationInspector.ReadCodexToml(text);
        Assert.Equal(ObservationStatus.Failed, read.Status);
        Assert.Equal("CONFIG_SYNTAX_INVALID", read.ErrorCode);
    }
}
