namespace OutboxFaultLab.Tests;

public class MetricsTests
{
    [Theory]
    [InlineData("db-first--baseline", 1L)]
    [InlineData("publish-first--baseline", -1L)]
    [InlineData("outbox--baseline", 6L)]
    public void LatencyUsesCommitAndAcceptance(string id, long expected)
    { Assert.Equal(expected, CaseExecutor.Run(ScenarioCatalog.Matrix().Single(s => s.Id == id)).Metrics.DeliveryLatencyTicks); }
    [Fact]
    public void RecoveryAndAttemptAccountingUseDurableBoundaries()
    {
        var cases = ScenarioCatalog.Matrix();
        var r = CaseExecutor.Run(cases.Single(s => s.Id == "outbox--producer--producer-commit-after"));
        Assert.Equal(9, r.Metrics.DeliveryLatencyTicks); Assert.Equal(6, r.Metrics.RecoveryLatencyTicks); Assert.Equal(7, r.Metrics.RecoveryDrainTicks);
        r = CaseExecutor.Run(cases.Single(s => s.Id == "outbox--dispatcher--publish-after-accept"));
        Assert.Null(r.Metrics.RecoveryLatencyTicks); Assert.Equal(7, r.Metrics.RecoveryDrainTicks);
        r = CaseExecutor.Run(cases.Single(s => s.Id == "outbox--producer--outbox-insert-after"));
        Assert.Equal(2, r.Metrics.AttemptedRowMutations); Assert.Equal(0, r.Metrics.CommittedRowMutations);
    }
}
