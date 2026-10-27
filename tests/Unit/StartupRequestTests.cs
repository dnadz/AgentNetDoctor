using AgentNetDoctor.Core.Launch;

namespace AgentNetDoctor.UnitTests;

public sealed class StartupRequestTests
{
    [Fact]
    public void LaunchAcceptsOnlyOpaqueProfileId()
    {
        var id = Guid.NewGuid().ToString("N");
        Assert.Equal(new StartupRequest(StartupMode.Launch, id), StartupRequest.Parse(["--launch", id]));
        Assert.Equal(StartupMode.Interactive, StartupRequest.Parse([]).Mode);
        Assert.Equal(StartupMode.Invalid, StartupRequest.Parse(["--launch", "C:\\external-plan.json"]).Mode);
        Assert.Equal(StartupMode.Invalid, StartupRequest.Parse(["--launch", id, "--force"]).Mode);
        Assert.Equal(StartupMode.Invalid, StartupRequest.Parse(["--unknown"]).Mode);
    }
}
