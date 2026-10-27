using AgentNetDoctor.Core.Adapters;
using AgentNetDoctor.Core.Repair;

namespace AgentNetDoctor.UnitTests;

public sealed class RepairPlanTests
{
    private static readonly AdapterCapabilities VerifiedFixture = new(true, ["http.proxy"], "synthetic verified fixture");

    [Fact]
    public void PolicyUnknownAndProviderChangesCannotProducePlan()
    {
        Assert.Null(RepairPlanner.PlanAppFields("synthetic", null, VerifiedFixture, "allowed", "hash", [new("http.proxy", null, "http://127.0.0.1:8080")], false));
        Assert.Null(RepairPlanner.PlanAppFields("synthetic", "1", VerifiedFixture, "allowed", "hash", [new("http.proxy", null, "http://127.0.0.1:8080")], true));
        Assert.Null(RepairPlanner.PlanAppFields("synthetic", "1", VerifiedFixture, "allowed", "hash", [new("model_provider", null, "synthetic")], false));
        Assert.Null(RepairPlanner.PlanAppFields("synthetic", "1", VerifiedFixture, "../outside", "hash", [new("http.proxy", null, "http://127.0.0.1:8080")], false));
    }

    [Fact]
    public void ConfirmationIsBoundToExactDifferencesAndVersion()
    {
        var plan = RepairPlanner.PlanAppFields("synthetic", "1", VerifiedFixture, "allowed", "hash",
            [new("http.proxy", null, "http://127.0.0.1:8080")], false);
        Assert.NotNull(plan);
        var confirmation = RepairPlanConfirmation.FromReviewedPlan(plan);
        Assert.True(RepairPlanConfirmation.Matches(plan, confirmation));
        Assert.False(RepairPlanConfirmation.Matches(plan with { TargetVersion = "2" }, confirmation));
        Assert.False(RepairPlanConfirmation.Matches(plan with { Changes = [new("http.proxy", null, "http://127.0.0.1:9090")] }, confirmation));
    }
}
