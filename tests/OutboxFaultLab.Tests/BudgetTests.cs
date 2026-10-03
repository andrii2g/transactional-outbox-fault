namespace OutboxFaultLab.Tests;

public class BudgetTests
{
    private static Scenario Baseline() => ScenarioCatalog.Matrix().Single(s => s.Id == "outbox--baseline") with { Expected = new Dictionary<string, System.Text.Json.JsonElement>() };
    [Fact]
    public void LastAllowedPublicationCanStillCommitMarker()
    {
        var s = Baseline(); var r = CaseExecutor.Run(s with { Budgets = new(0, 1, 8) });
        Assert.Equal("completed", r.ExecutionStatus); Assert.Equal(1, r.Metrics.PublishAttempts); Assert.Equal(0, r.Metrics.PendingOutboxRows);
    }
    [Theory]
    [InlineData(1, 0)]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    public void TickBudgetLeavesDurableIntentAndRollsBackMarker(long maxTicks, int accepted)
    {
        var r = CaseExecutor.Run(Baseline() with { Budgets = new(0, 10, maxTicks) });
        Assert.Equal("unresolved", r.ExecutionStatus); Assert.Equal(accepted, r.Metrics.AcceptedDeliveries); Assert.Equal(1, r.Metrics.PendingOutboxRows); Assert.Equal(0, r.Metrics.LostEvents);
        Assert.Equal(2, r.Metrics.CommittedRowMutations); Assert.Null(r.Snapshots[^1].Database.Outbox.Single().SentTick);
    }
    [Fact]
    public void RestartBudgetPreservesAcceptedPendingStateAndNeverReplaysProducer()
    {
        var s = Baseline(); s = s with { Budgets = new(0, 10, 1000), Faults = [new(Component.Dispatcher, "publish.after-accept", s.Command.EventId, 1)] };
        var r = CaseExecutor.Run(s); Assert.Equal("unresolved", r.ExecutionStatus); Assert.Equal(0, r.Metrics.RestartCount); Assert.Equal(1, r.Metrics.AcceptedDeliveries); Assert.Equal(1, r.Metrics.PendingOutboxRows);
        Assert.Single(r.Trace, e => e.Point == "order-insert.before"); Assert.Equal("passed", r.Verification);
    }
    [Fact]
    public void BackoffSaturatesWithoutOverflowAndDomainTimingsRejectInvalidValues()
    {
        Assert.Equal(long.MaxValue, RetryPolicy.Delay(new(1, 1, 1, 1, long.MaxValue / 2, long.MaxValue), int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimingSpec(1, -1, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetSpec(-1, 1, 1));
    }
}
