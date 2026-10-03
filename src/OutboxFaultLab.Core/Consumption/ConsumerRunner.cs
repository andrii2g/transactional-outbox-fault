namespace OutboxFaultLab;

public sealed record ProjectionRow(string OrderId, long AppliedCount, long AppliedAmountCents);
public sealed record ProcessedRow(string EventId, string OrderId, string PayloadHash, long ProcessedTick);
public sealed record ConsumerSnapshot(IReadOnlyList<ProjectionRow> Projection, IReadOnlyList<ProcessedRow> ProcessedEvents);
public sealed record ConsumerObservation(string Phase, long Tick, ConsumerSnapshot Database);
public sealed record ConsumerResult(string Mode, long DuplicateSkipped, long RestartCount, ConsumerSnapshot Final,
    IReadOnlyList<ConsumerObservation> Snapshots, IReadOnlyList<TraceEvent> Trace, int FiredFaults);
public interface IConsumerStore
{
    bool Apply(OrderCreated message, bool idempotent, long tick, Action<string> hook);
    ConsumerSnapshot ReadSnapshot();
}
public sealed class ConsumerRunner
{
    public ConsumerResult Run(IReadOnlyList<BrokerDelivery> journal, bool idempotent,
        Func<TraceSink, IConsumerStore> stores, IReadOnlyList<FaultSpec>? schedule = null)
    {
        var clock = new LogicalClock(); var trace = new TraceSink(clock); var faults = new FaultInjector(schedule ?? [], trace);
        long skipped = 0; var index = 0; var store = stores(trace); List<ConsumerObservation> observations = [];
        Capture("initial");
        while (index < journal.Count)
        {
            var message = journal[index].Event;
            trace.Append("ConsumerDelivery", Component.Consumer, eventId: message.EventId);
            try
            {
                if (!store.Apply(message, idempotent, clock.Tick, p =>
                {
                    if (p == "commit.before") clock.Advance(1);
                    faults.Reach(new(Component.Consumer, p, message.EventId, clock.Tick, trace.Epoch, "consumer-tx-" + (index + 1)));
                })) { skipped++; trace.Append("DuplicateSkipped", Component.Consumer, eventId: message.EventId); }
                index++;
            }
            catch (SimulatedCrashException)
            {
                Capture("post-stop"); clock.Advance(3); trace.Epoch++; trace.Append("RuntimeRestarted", Component.Consumer);
                store = stores(trace); Capture("post-restart"); index = 0;
            }
        }
        if (faults.FiredCount != (schedule?.Count ?? 0)) throw new InvalidOperationException("Consumer fault not reached.");
        Capture("final");
        return new(idempotent ? "idempotent" : "naive", skipped, trace.Epoch, store.ReadSnapshot(), observations.AsReadOnly(), trace.Events, faults.FiredCount);
        void Capture(string phase) => observations.Add(new(phase, clock.Tick, store.ReadSnapshot()));
    }
}
