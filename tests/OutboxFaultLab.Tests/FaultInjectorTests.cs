namespace OutboxFaultLab.Tests;

public class FaultInjectorTests
{
    [Fact]
    public void CountersSurviveEpochsAndMatchOnlyScheduledIdentity()
    {
        var clock = new LogicalClock(); var trace = new TraceSink(clock);
        var faults = new FaultInjector([new(Component.Dispatcher, "publish.before", "E", 1), new(Component.Dispatcher, "publish.before", "E", 2)], trace);
        faults.Reach(new(Component.Producer, "publish.before", "E", 0, 0, null));
        faults.Reach(new(Component.Dispatcher, "publish.before", "other", 0, 0, null));
        for (var i = 0; i < 2; i++) { trace.Epoch = i; Assert.Throws<SimulatedCrashException>(() => faults.Reach(new(Component.Dispatcher, "publish.before", "E", 0, i, null))); }
        faults.Reach(new(Component.Dispatcher, "publish.before", "E", 0, 2, null));
        Assert.Equal(2, faults.FiredCount); Assert.Equal(Enumerable.Range(1, trace.Events.Count).Select(i => (long)i), trace.Events.Select(e => e.Sequence));
    }
    [Fact]
    public void ClockChecksOverflowAndNegativeDurations()
    { var c = new LogicalClock(); Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(-1)); c.Advance(long.MaxValue); Assert.Throws<OverflowException>(() => c.Advance(1)); }
    [Fact]
    public void CommandsValidateIdsAndAmounts()
    { Assert.Throws<ArgumentException>(() => new CreateOrder("../bad", "E", 1)); Assert.Throws<ArgumentOutOfRangeException>(() => new CreateOrder("O", "E", -1)); }
}
