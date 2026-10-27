using AgentNetDoctor.Core.Adapters;

namespace AgentNetDoctor.UnitTests;

public sealed class WindowsPathPolicyTests
{
    [Theory]
    [InlineData(@"\\server\share\settings.json")]
    [InlineData(@"\\?\UNC\server\share\settings.json")]
    [InlineData(@"\\?\C:\settings.json")]
    [InlineData(@"C:relative\settings.json")]
    [InlineData(@"C:\settings.json:stream")]
    public void RemoteDeviceRelativeAndAlternateStreamPathsAreRejected(string path) =>
        Assert.False(WindowsPathPolicy.IsLocalDriveSyntax(path));

    [Fact]
    public void OrdinaryLocalAbsolutePathIsAccepted() =>
        Assert.True(WindowsPathPolicy.IsLocalDriveSyntax(@"C:\synthetic folder\settings.json"));
}
