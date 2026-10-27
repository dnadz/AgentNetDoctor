using System.Security.Cryptography;
using System.Text;
using AgentNetDoctor.Core.Repair;

namespace AgentNetDoctor.UnitTests;

public sealed class ProxyFieldPatcherTests
{
    [Fact]
    public void PatchesOnlySelectedValueAndPreservesBomCommentsUnknownFieldsAndLineEndings()
    {
        const string before = "{ // synthetic fixture\r\n  \"http.proxy\": \"http://127.0.0.1:8080\",\r\n  \"unknown\": [1, 2],\r\n}\r\n";
        var original = WithBom(before);
        var plan = Plan(original, "http://127.0.0.1:8080", "http://127.0.0.1:9090");
        Assert.Equal(WithBom(before.Replace(":8080", ":9090")), ProxyFieldPatcher.Patch(original, plan));
    }

    [Fact]
    public void DoesNotPatchMismatchedOriginalOrAcceptSecretAndProviderChange()
    {
        var original = WithBom("{\"http.proxy\":\"http://127.0.0.1:8080\"}");
        var plan = Plan(original, "different-before", "http://127.0.0.1:9090");
        Assert.Throws<InvalidOperationException>(() => ProxyFieldPatcher.Patch(original, plan));
        plan = Plan(original, "http://127.0.0.1:8080", "http://user:synthetic-password@127.0.0.1:9090");
        Assert.Throws<InvalidOperationException>(() => ProxyFieldPatcher.Patch(original, plan));
        plan = plan with { Changes = [new("model_provider", null, "synthetic")] };
        Assert.Throws<InvalidOperationException>(() => ProxyFieldPatcher.Patch(original, plan));
    }

    private static byte[] WithBom(string text) => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
    private static RepairPlan Plan(byte[] before, string oldValue, string newValue) => new("synthetic", ActionKind.PatchAppFields, "antigravity", "fixture", "synthetic-version",
        Convert.ToHexString(SHA256.HashData(before)), [new("http.proxy", oldValue, newValue)], "fixture", "fixture", "fixture");
}
