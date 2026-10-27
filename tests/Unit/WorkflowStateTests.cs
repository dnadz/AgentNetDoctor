using AgentNetDoctor.Core.Workflow;

namespace AgentNetDoctor.UnitTests;

public sealed class WorkflowStateTests
{
    [Fact]
    public void CannotProbeWithoutConsentOrSnapshot()
    {
        var session = new CheckSession();
        Assert.Throws<InvalidOperationException>(() => session.StartProbes(true));
        session.StartCollection();
        session.FinishCollection();
        Assert.Throws<InvalidOperationException>(() => session.StartProbes(false));
        Assert.Equal(WorkflowState.AwaitingProbeConsent, session.State);
        session.StartProbes(true);
        Assert.Equal(WorkflowState.Probing, session.State);
        session.Fail();
        Assert.False(session.CanProbe);
    }
}
