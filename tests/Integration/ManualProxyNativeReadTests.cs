using AgentNetDoctor.Windows.Repair;

namespace AgentNetDoctor.IntegrationTests;

public sealed class ManualProxyNativeReadTests
{
    [Fact]
    public void UnvalidatedSystemWriterIsLockedInReleaseBuild() => Assert.False(WindowsManualProxySettings.WritesValidated);
    [Fact]
    public void ActualWriteEntryCannotBypassReleaseGate() => Assert.Throws<NotSupportedException>(() =>
        new WindowsManualProxySettings().CompareExchangeFlags(new(3, "127.0.0.1:7890", "", "", false), 1));
    [Fact]
    public void CurrentUserSettingsCanBeQueriedWithoutMutation()
    {
        var state = new WindowsManualProxySettings().Read();
        Assert.InRange(state.Flags, 0, 31);
        Assert.NotNull(state.Server);
        Assert.NotNull(state.Bypass);
        Assert.NotNull(state.AutoConfigUrl);
    }
}
