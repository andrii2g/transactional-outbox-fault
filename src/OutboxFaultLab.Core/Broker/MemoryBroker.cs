namespace OutboxFaultLab;

public sealed class MemoryBroker(BrokerPlan plan, TimingSpec timing, BudgetSpec budget,
    LogicalClock clock, TraceSink trace, IFaultInjector faults) : IBroker
{
    private int cursor;
    private readonly List<BrokerDelivery> journal = [];
    public IReadOnlyList<BrokerDelivery> Journal => journal.AsReadOnly();
    public int Attempts { get; private set; }
    public void Publish(OrderCreated message, Component component)
    {
        if (Attempts >= budget.MaxPublishAttempts) throw new BudgetExhaustedException();
        Attempts++; trace.Append("PublishInvoked", component, eventId: message.EventId);
        Hook("publish.before"); clock.Advance(timing.PublishTicks);
        var outcome = cursor < plan.Outcomes.Count ? plan.Outcomes[cursor] : plan.DefaultOutcome; cursor++;
        if (outcome == BrokerOutcome.Reject) { trace.Append("BrokerRejected", component, eventId: message.EventId); throw new BrokerUnavailableException(); }
        journal.Add(new(journal.Count + 1L, message with { }, clock.Tick, trace.Epoch));
        trace.Append("BrokerAccepted", component, eventId: message.EventId); Hook("publish.after-accept");
        if (outcome == BrokerOutcome.AcceptAckLost) { trace.Append("AckLost", component, eventId: message.EventId); throw new BrokerAcknowledgementLostException(); }
        trace.Append("BrokerAck", component, eventId: message.EventId); Hook("publish.after-ack");
        void Hook(string point) => faults.Reach(new(component, point, message.EventId, clock.Tick, trace.Epoch, null));
    }
}
