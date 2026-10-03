namespace OutboxFaultLab.Tests;

public class DispatcherRecoveryTests
{
    [Theory]
    [InlineData("sent-update-after", false)]
    [InlineData("sent-commit-before", false)]
    [InlineData("sent-commit-after", true)]
    public void MarkerCommitControlsRepublishAfterFreshRuntime(string point, bool sent)
    {
        var r = CaseExecutor.Run(ScenarioCatalog.Matrix().Single(s => s.Id == "outbox--dispatcher--" + point));
        var stopped = r.Snapshots.Single(s => s.Phase == "post-stop"); Assert.Equal(sent, stopped.Database.Outbox.Single().SentTick.HasValue);
        var restarted = r.Snapshots.Single(s => s.Phase == "post-restart"); Assert.Equal(stopped.Database.Orders, restarted.Database.Orders); Assert.Equal(stopped.Database.Outbox, restarted.Database.Outbox);
        Assert.Equal(sent ? 1 : 2, r.Deliveries.Count); Assert.Equal(1, r.Metrics.CommittedOrders);
    }
}
