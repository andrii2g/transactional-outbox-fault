using System.Text.Json;
namespace OutboxFaultLab;

public sealed class TransactionIds
{ private long sent; public string NextSent() => "sent-tx-" + (++sent).ToString(System.Globalization.CultureInfo.InvariantCulture); }
public sealed class OutboxDispatcher(IStore store, IBroker broker, LogicalClock clock, TraceSink trace,
    IFaultInjector faults, TimingSpec timing, TransactionIds ids)
{
    public DispatchResult DispatchOne()
    {
        var row = store.ReadOldestUnsent(); if (row is null) return DispatchResult.Empty;
        var message = JsonSerializer.Deserialize<OrderCreated>(row.PayloadJson, Wire.Json) ?? throw new InvalidOperationException("Invalid persisted event.");
        try { broker.Publish(message, Component.Dispatcher); }
        catch (BrokerTransientException) { return DispatchResult.RetryRequired; }
        var id = ids.NextSent(); using var tx = store.BeginTransaction(id);
        Hook("sent-update.before"); tx.MarkSent(row.EventId, clock.Tick); Hook("sent-update.after");
        Hook("sent-commit.before"); clock.Advance(timing.CommitTicks); tx.Commit(); Hook("sent-commit.after");
        return DispatchResult.Delivered;
        void Hook(string p) => faults.Reach(new(Component.Dispatcher, p, row.EventId, clock.Tick, trace.Epoch, id));
    }
}
