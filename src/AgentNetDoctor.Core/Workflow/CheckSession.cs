namespace AgentNetDoctor.Core.Workflow;

public enum WorkflowState { Idle, Collecting, AwaitingProbeConsent, Probing, Partial, Failed }

public sealed class CheckSession
{
    public WorkflowState State { get; private set; }
    public bool CanProbe => State is WorkflowState.AwaitingProbeConsent or WorkflowState.Partial;
    public void StartCollection() => State = WorkflowState.Collecting;
    public void FinishCollection() => State = WorkflowState.AwaitingProbeConsent;
    public void StartProbes(bool consent)
    {
        if (!consent || State is not (WorkflowState.AwaitingProbeConsent or WorkflowState.Partial))
            throw new InvalidOperationException("请先完成本机检查并确认联网范围");
        State = WorkflowState.Probing;
    }
    public void Finish() => State = WorkflowState.Partial;
    public void Fail() => State = WorkflowState.Failed;
}
