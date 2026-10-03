namespace OutboxFaultLab;

public sealed class ExperimentRunner
{
    public RunResult Run(Scenario s, Func<TraceSink, IStore> storeFactory)
    {
        var clock = new LogicalClock(s.Budgets.MaxTicks); var trace = new TraceSink(clock);
        var faults = new FaultInjector(s.Faults, trace);
        var broker = new MemoryBroker(s.Broker, s.Timing, s.Budgets, clock, trace, faults);
        var runtime = new RuntimeFactory(() => storeFactory(trace), broker, clock, trace, faults, s.Timing, new());
        List<Snapshot> snapshots = []; var exhausted = false; var stopped = false;
        Capture("initial");
        try { runtime.Producer(s.Strategy).Execute(s.Command); }
        catch (SimulatedCrashException) { stopped = !Restart(); }
        catch (BrokerTransientException) { trace.Append("ProducerStopped", Component.Producer, eventId: s.Command.EventId); Capture("post-stop"); }
        catch (BudgetExhaustedException) { exhausted = true; stopped = true; trace.Append("BudgetExhausted"); Capture("post-stop"); }
        if (s.Strategy == StrategyKind.Outbox && !stopped)
        {
            var dispatcher = runtime.Dispatcher(); var failures = 0; var delay = s.Timing.PollTicks;
            while (storeFactory(trace).ReadOldestUnsent() is not null)
            {
                try
                {
                    if (broker.Attempts >= s.Budgets.MaxPublishAttempts) throw new BudgetExhaustedException();
                    clock.Advance(delay);
                    var result = dispatcher.DispatchOne();
                    if (result == DispatchResult.RetryRequired) { failures++; delay = RetryPolicy.Delay(s.Timing, failures); }
                    else { failures = 0; delay = s.Timing.PollTicks; }
                }
                catch (SimulatedCrashException)
                {
                    if (!Restart()) { exhausted = true; break; }
                    dispatcher = runtime.Dispatcher(); failures = 0; delay = s.Timing.PollTicks;
                }
                catch (BudgetExhaustedException) { trace.Append("BudgetExhausted"); exhausted = true; break; }
            }
        }
        Capture("final"); var final = snapshots[^1].Database;
        var metrics = MetricsCalculator.Calculate(s, final, broker.Journal, trace.Events, snapshots);
        var status = exhausted || (s.Strategy == StrategyKind.Outbox && metrics.PendingOutboxRows > 0) ? "unresolved" : "completed";
        var (comparisons, errors) = RunVerifier.Verify(s, final, broker.Journal, metrics, status, faults.FiredCount);
        return new(s.Id, Wire.Strategy(s.Strategy), status, metrics.GhostEvents > 0 ? "ghost" : metrics.MissingEvents > 0 ? "missing" : "consistent",
            errors.Count == 0 && comparisons.All(c => c.Passed) ? "passed" : "failed", metrics, comparisons, errors, snapshots.AsReadOnly(),
            trace.Events, broker.Journal, s.Faults.Count, faults.FiredCount);
        void Capture(string phase)
        {
            var db = storeFactory(trace).ReadCommittedSnapshot();
            var accepted = broker.Journal.Select(d => d.Event.EventId).ToHashSet(StringComparer.Ordinal);
            var missing = db.Orders.Count(o => o.OrderId == s.Command.OrderId && !accepted.Contains(s.Command.EventId));
            var ghost = broker.Journal.Where(d => !db.Orders.Any(o => o.OrderId == d.Event.OrderId)).Select(d => d.Event.EventId).Distinct(StringComparer.Ordinal).LongCount();
            snapshots.Add(new(phase, clock.Tick, trace.Epoch, db, broker.Journal.Count, missing, ghost, db.Outbox.LongCount(o => o.SentTick is null)));
        }
        bool Restart()
        {
            Capture("post-stop");
            if (trace.Epoch >= s.Budgets.MaxRestarts) { exhausted = true; return false; }
            try { clock.Advance(s.Timing.RestartTicks); }
            catch (BudgetExhaustedException) { exhausted = true; return false; }
            trace.Epoch++; trace.Append("RuntimeRestarted"); Capture("post-restart"); return true;
        }
    }
}
