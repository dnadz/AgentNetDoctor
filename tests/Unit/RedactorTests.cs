using AgentNetDoctor.Core.Reporting;

namespace AgentNetDoctor.UnitTests;

public sealed class RedactorTests
{
    [Theory]
    [InlineData("{\"api_key\":\"synthetic-secret-value\"}", "synthetic-secret-value")]
    [InlineData("Authorization: Bearer synthetic-secret-value", "synthetic-secret-value")]
    [InlineData("https://user:synthetic-secret-value@proxy.invalid", "synthetic-secret-value")]
    [InlineData("Bearer synthetic-secret-value", "synthetic-secret-value")]
    public void RemovesSecretsFromSupportedTextForms(string input, string secret)
    {
        Assert.DoesNotContain(secret, Redactor.Redact(input));
    }
}
