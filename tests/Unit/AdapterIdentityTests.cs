using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.UnitTests;

public sealed class AdapterIdentityTests
{
    [Fact]
    public void CodeBuddyIsNotWorkBuddyAndNameSuffixCannotSpoofProduct()
    {
        Assert.False(ProductIdentity.Matches("workbuddy", new("CodeBuddy 5.9.0", "5.9.0", "registry")));
        Assert.False(ProductIdentity.Matches("workbuddy", new("WorkBuddy Helper", "5.9.0", "registry")));
        Assert.True(ProductIdentity.Matches("workbuddy", new("WorkBuddy 5.9.0", "5.9.0", "registry")));
        Assert.True(ProductIdentity.Matches("antigravity", new("Antigravity 2.26.0", "2.26.0", "registry")));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public void UnknownVersionCannotWrite(string? version)
    {
        Assert.Empty(CapabilityPolicy.ForVersion(version, new HashSet<string>()).WritableFields);
    }
}
