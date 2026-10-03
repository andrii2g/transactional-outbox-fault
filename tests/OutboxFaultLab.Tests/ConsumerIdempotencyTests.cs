namespace OutboxFaultLab.Tests;

public class ConsumerIdempotencyTests
{
    private static BrokerDelivery Delivery(string id = "E", string order = "O", long amount = 1250) => new(1, new(1, id, order, amount), 1, 0);
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void DuplicateDeliveriesHaveSeparateTransactionalEffects(bool idempotent, int count)
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(true);
        var r = new ConsumerRunner().Run([Delivery(), Delivery()], idempotent, t => new SqliteConsumerStore(f, t));
        Assert.Equal(count, r.Final.Projection.Single().AppliedCount); Assert.Equal(count * 1250, r.Final.Projection.Single().AppliedAmountCents);
    }
    [Theory]
    [InlineData("inbox-insert.before", false)]
    [InlineData("inbox-insert.after", false)]
    [InlineData("projection-update.before", false)]
    [InlineData("projection-update.after", false)]
    [InlineData("commit.before", false)]
    [InlineData("commit.after", true)]
    public void CrashAndReplayPreserveAtomicInboxAndEffect(string point, bool committed)
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(true);
        var r = new ConsumerRunner().Run([Delivery(), Delivery()], true, t => new SqliteConsumerStore(f, t), [new(Component.Consumer, point, "E", 1)]);
        var stopped = r.Snapshots.Single(s => s.Phase == "post-stop").Database;
        Assert.Equal(committed ? 1 : 0, stopped.ProcessedEvents.Count); Assert.Equal(stopped.ProcessedEvents.Count, stopped.Projection.Count);
        Assert.Equal(1, r.Final.Projection.Single().AppliedCount); Assert.Equal(1, r.FiredFaults);
    }
    [Fact]
    public void DistinctEventsApplyAndCollisionsFail()
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(true); var runner = new ConsumerRunner();
        var r = runner.Run([Delivery(), Delivery("E2", "O2")], true, t => new SqliteConsumerStore(f, t)); Assert.Equal(2, r.Final.Projection.Count);
        Assert.Throws<InvalidOperationException>(() => runner.Run([Delivery(amount: 99)], true, t => new SqliteConsumerStore(f, t)));
    }
    [Fact]
    public void ArithmeticOverflowRollsBackInboxAndProjection()
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(true); var runner = new ConsumerRunner();
        runner.Run([Delivery(amount: long.MaxValue)], true, t => new SqliteConsumerStore(f, t));
        Assert.Throws<OverflowException>(() => runner.Run([Delivery("E2", amount: 1)], true, t => new SqliteConsumerStore(f, t)));
        var snapshot = new SqliteConsumerStore(f, new(new LogicalClock())).ReadSnapshot(); Assert.Single(snapshot.ProcessedEvents); Assert.Equal(long.MaxValue, snapshot.Projection.Single().AppliedAmountCents);
    }
}
