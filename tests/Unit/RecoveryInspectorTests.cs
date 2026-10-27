using AgentNetDoctor.Core.Repair;

namespace AgentNetDoctor.UnitTests;

public sealed class RecoveryInspectorTests
{
    [Theory]
    [InlineData("before", RecoveryOutcome.SourceStateObserved)]
    [InlineData("after", RecoveryOutcome.ExpectedStateObserved)]
    [InlineData("external-or-partial", RecoveryOutcome.Conflict)]
    public async Task PreparedRecordIsClassifiedWithoutWriting(string hash, RecoveryOutcome expected)
    {
        var plan = new RepairPlan("synthetic", ActionKind.PatchAppFields, "antigravity", "fixture", "1", "before", [], "", "", "");
        var entry = new RepairJournalEntry(plan.Id, plan, "synthetic", MutationDirection.Apply, "before", "after", JournalStage.Prepared);
        var result = await RecoveryInspector.InspectAsync(entry, new ReadOnlyTarget(hash), CancellationToken.None);
        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task UnsupportedTargetCannotBeReadByRecoveryRequest()
    {
        var plan = new RepairPlan("synthetic", ActionKind.PatchAppFields, "antigravity", "outside", "1", "before", [], "", "", "");
        var entry = new RepairJournalEntry(plan.Id, plan, "synthetic", MutationDirection.Apply, "before", "after", JournalStage.Prepared);
        Assert.Equal(RecoveryOutcome.Unavailable, (await RecoveryInspector.InspectAsync(entry, new ReadOnlyTarget("before"), CancellationToken.None)).Outcome);
    }

    private sealed class ReadOnlyTarget(string fingerprint) : IRepairTargetStore
    {
        public bool Supports(RepairPlan plan) => plan.TargetId == "fixture";
        public Task<string> ReadFingerprintAsync(RepairPlan plan, CancellationToken cancellationToken)
        {
            Assert.True(Supports(plan)); return Task.FromResult(fingerprint);
        }
        public Task<byte[]> ReadOriginalAsync(RepairPlan plan, CancellationToken cancellationToken) => throw new InvalidOperationException("must not read raw configuration");
        public Task<string?> ApplyIfMatchesAsync(RepairPlan plan, CancellationToken cancellationToken) => throw new InvalidOperationException("must not mutate");
        public Task<bool> RestoreIfMatchesAsync(RepairPlan plan, string expectedFingerprint, byte[] original, CancellationToken cancellationToken) => throw new InvalidOperationException("must not mutate");
    }
}
