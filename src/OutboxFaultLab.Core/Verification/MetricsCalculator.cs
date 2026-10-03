using System.Globalization;
namespace OutboxFaultLab;

public static class MetricsCalculator
{
    public static Metrics Calculate(Scenario s, DatabaseSnapshot db, IReadOnlyList<BrokerDelivery> journal,
        IReadOnlyList<TraceEvent> trace, IReadOnlyList<Snapshot> snapshots)
    {
        var accepted = journal.Select(d => d.Event.EventId).ToHashSet(StringComparer.Ordinal);
        var missing = db.Orders.Count(o => o.OrderId == s.Command.OrderId && !accepted.Contains(s.Command.EventId));
        var pending = db.Outbox.Where(o => o.SentTick is null).ToArray();
        var recoverable = missing > 0 && pending.Any(o => o.EventId == s.Command.EventId) ? 1L : 0L;
        var orderIds = db.Orders.Select(o => o.OrderId).ToHashSet(StringComparer.Ordinal);
        var ghost = journal.Where(d => !orderIds.Contains(d.Event.OrderId)).Select(d => d.Event.EventId).Distinct(StringComparer.Ordinal).LongCount();
        var commits = trace.Where(t => t.Kind == "TransactionCommitted").ToArray();
        var mutations = commits.Sum(t => long.Parse(t.Details["mutations"], CultureInfo.InvariantCulture));
        var calls = trace.Where(t => t.Kind == "PublishInvoked").ToArray();
        var firstAccept = journal.FirstOrDefault(d => d.Event.EventId == s.Command.EventId);
        var producerCommit = commits.FirstOrDefault(t => t.TransactionId == "producer-tx-1");
        var missingRestart = snapshots.FirstOrDefault(t => t.Phase == "post-restart" && t.MissingEvents > 0);
        var pendingRestart = snapshots.FirstOrDefault(t => t.Phase == "post-restart" && t.PendingOutboxRows > 0);
        var lastMarker = commits.LastOrDefault(t => t.TransactionId?.StartsWith("sent-tx-", StringComparison.Ordinal) == true);
        return new(db.Orders.Count, journal.Count, accepted.Count, missing, recoverable, missing - recoverable, ghost,
            journal.Count - accepted.Count, pending.Length, calls.Length,
            calls.GroupBy(t => t.EventId, StringComparer.Ordinal).Sum(g => Math.Max(0, g.LongCount() - 1)),
            trace.LongCount(t => t.Kind == "RuntimeRestarted"), mutations, trace.LongCount(t => t.Kind == "MutationExecuted"),
            mutations - db.Orders.Count, commits.Length,
            firstAccept is not null && producerCommit is not null ? firstAccept.AcceptedTick - producerCommit.Tick : null,
            missingRestart is not null && firstAccept is not null ? firstAccept.AcceptedTick - missingRestart.Tick : null,
            pendingRestart is not null && pending.Length == 0 && lastMarker is not null ? lastMarker.Tick - pendingRestart.Tick : null);
    }
}
