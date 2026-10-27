using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Models;

namespace AgentNetDoctor.UnitTests;

public sealed class RuntimeRequirementTests
{
    [Fact]
    public void NativeInstallationDoesNotRequireExternalNode()
    {
        var missing = new Observation<string>(ObservationStatus.Failed, null, "NODE_NOT_FOUND", DateTimeOffset.UtcNow);
        Assert.Equal(ObservationStatus.NotApplicable, RuntimeRequirements.CheckClaudeNode("2.1.296", "native", missing).Status);
    }

    [Theory]
    [InlineData("v20.19.0", ObservationStatus.Failed)]
    [InlineData("v22.0.0", ObservationStatus.Success)]
    [InlineData("24.1.0", ObservationStatus.Success)]
    public void KnownNpmManifestRequiresNode22(string version, ObservationStatus expected)
    {
        Assert.Equal(expected, RuntimeRequirements.CheckClaudeNode("2.1.296", "npm",
            new(ObservationStatus.Success, version, null, DateTimeOffset.UtcNow)).Status);
    }

    [Fact]
    public void AccessDeniedIsNotMissingAndUnknownVersionDoesNotGuessRequirements()
    {
        var denied = new Observation<string>(ObservationStatus.PermissionDenied, null, "ACCESS_DENIED", DateTimeOffset.UtcNow);
        Assert.Equal(ObservationStatus.PermissionDenied, RuntimeRequirements.CheckClaudeNode("2.1.296", "npm", denied).Status);
        Assert.Equal(ObservationStatus.NotTested, RuntimeRequirements.CheckClaudeNode("unknown", "npm", denied).Status);
    }
}
