namespace OutboxFaultLab;

public sealed class ProducerTransaction(IStore store, LogicalClock clock, TraceSink trace, IFaultInjector faults, TimingSpec timing)
{
    public void Execute(CreateOrder c, bool outbox)
    {
        const string id = "producer-tx-1";
        using var tx = store.BeginTransaction(id);
        Hook("order-insert.before"); tx.InsertOrder(c); Hook("order-insert.after");
        if (outbox) { Hook("outbox-insert.before"); tx.InsertOutbox(c.ToEvent(), Wire.Payload(c.ToEvent()), clock.Tick); Hook("outbox-insert.after"); }
        Hook("producer-commit.before"); clock.Advance(timing.CommitTicks); tx.Commit(); Hook("producer-commit.after");
        void Hook(string p) => faults.Reach(new(Component.Producer, p, c.EventId, clock.Tick, trace.Epoch, id));
    }
}
