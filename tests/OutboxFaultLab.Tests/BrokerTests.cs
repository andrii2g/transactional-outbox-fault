namespace OutboxFaultLab.Tests;

public class BrokerTests
{
    [Fact]
    public void RejectionLostAckAndDuplicateAcceptanceAreDistinct()
    {
        var clock = new LogicalClock(); var trace = new TraceSink(clock); var faults = new FaultInjector([], trace);
        var broker = new MemoryBroker(new([BrokerOutcome.Reject, BrokerOutcome.AcceptAckLost], BrokerOutcome.AcceptAck), new(1, 1, 5, 3, 2, 8), new(4, 10, 1000), clock, trace, faults);
        var e = new CreateOrder("O", "E", 1).ToEvent();
        Assert.Throws<BrokerUnavailableException>(() => broker.Publish(e, Component.Producer)); Assert.Empty(broker.Journal);
        Assert.Throws<BrokerAcknowledgementLostException>(() => broker.Publish(e, Component.Producer)); Assert.Single(broker.Journal);
        broker.Publish(e, Component.Dispatcher); Assert.Equal(2, broker.Journal.Count); Assert.Equal(e, broker.Journal[0].Event);
        Assert.Equal(3, broker.Attempts); Assert.Single(trace.Events, t => t.Kind == "BrokerAck");
    }
}
