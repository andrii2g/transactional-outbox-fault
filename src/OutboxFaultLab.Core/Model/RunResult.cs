namespace OutboxFaultLab;

public sealed record Metrics(long CommittedOrders, long AcceptedDeliveries, long DistinctAcceptedEvents,
    long MissingEvents, long RecoverablePendingEvents, long LostEvents, long GhostEvents,
    long DuplicateDeliveries, long PendingOutboxRows, long PublishAttempts, long PublishRetries,
    long RestartCount, long CommittedRowMutations, long AttemptedRowMutations, long ExtraWrites,
    long TransactionCommits, long? DeliveryLatencyTicks, long? RecoveryLatencyTicks, long? RecoveryDrainTicks);
public sealed record Snapshot(string Phase, long Tick, int Epoch, DatabaseSnapshot Database,
    long AcceptedJournalCount, long MissingEvents, long GhostEvents, long PendingOutboxRows);
public sealed record Comparison(string Field, string Expected, string Actual, bool Passed);
public sealed record RunResult(string ScenarioId, string Strategy, string ExecutionStatus, string Consistency,
    string Verification, Metrics Metrics, IReadOnlyList<Comparison> Comparisons, IReadOnlyList<string> Errors,
    IReadOnlyList<Snapshot> Snapshots, IReadOnlyList<TraceEvent> Trace, IReadOnlyList<BrokerDelivery> Deliveries,
    int PlannedFaults, int FiredFaults);
