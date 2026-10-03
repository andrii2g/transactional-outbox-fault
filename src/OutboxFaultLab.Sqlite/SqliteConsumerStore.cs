using System.Security.Cryptography;
using System.Text;
namespace OutboxFaultLab;

public sealed class SqliteConsumerStore(SqliteConnectionFactory factory, TraceSink trace) : IConsumerStore
{
    public bool Apply(OrderCreated message, bool idempotent, long tick, Action<string> hook)
    {
        using var c = factory.Open(); using var tx = c.BeginTransaction(); var committed = false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Wire.Payload(message))));
        try
        {
            if (idempotent)
            {
                using (var q = c.CreateCommand())
                {
                    q.Transaction = tx; q.CommandText = "SELECT OrderId,PayloadHash FROM ProcessedEvents WHERE EventId=$event"; q.Parameters.AddWithValue("$event", message.EventId);
                    using var r = q.ExecuteReader();
                    if (r.Read())
                    {
                        if (r.GetString(0) != message.OrderId || r.GetString(1) != hash) throw new InvalidOperationException("Event identity collision: different payload.");
                        return false;
                    }
                }
                hook("inbox-insert.before");
                Execute("INSERT INTO ProcessedEvents(EventId,OrderId,PayloadHash,ProcessedTick) VALUES ($event,$order,$hash,$tick)",
                    ("$event", message.EventId), ("$order", message.OrderId), ("$hash", hash), ("$tick", tick));
                trace.Append("ConsumerMutation", Component.Consumer, "inbox-insert", message.EventId); hook("inbox-insert.after");
            }
            long count = 0, amount = 0;
            using (var q = c.CreateCommand())
            {
                q.Transaction = tx; q.CommandText = "SELECT AppliedCount,AppliedAmountCents FROM Projection WHERE OrderId=$order"; q.Parameters.AddWithValue("$order", message.OrderId);
                using var r = q.ExecuteReader(); if (r.Read()) { count = r.GetInt64(0); amount = r.GetInt64(1); }
            }
            count = checked(count + 1); amount = checked(amount + message.AmountCents);
            hook("projection-update.before");
            Execute("INSERT INTO Projection(OrderId,AppliedCount,AppliedAmountCents) VALUES ($order,$count,$amount) ON CONFLICT(OrderId) DO UPDATE SET AppliedCount=$count,AppliedAmountCents=$amount",
                ("$order", message.OrderId), ("$count", count), ("$amount", amount));
            trace.Append("ConsumerMutation", Component.Consumer, "projection-update", message.EventId); hook("projection-update.after");
            hook("commit.before"); tx.Commit(); committed = true; trace.Append("ConsumerCommitted", Component.Consumer, eventId: message.EventId); hook("commit.after"); return true;
            void Execute(string sql, params (string Name, object Value)[] values)
            {
                using var q = c.CreateCommand(); q.Transaction = tx; q.CommandText = sql;
                foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value);
                if (q.ExecuteNonQuery() != 1) throw new InvalidOperationException("Consumer mutation must affect one row.");
            }
        }
        finally { if (!committed) trace.Append("ConsumerRolledBack", Component.Consumer, eventId: message.EventId); }
    }
    public ConsumerSnapshot ReadSnapshot()
    {
        using var c = factory.Open(); using var tx = c.BeginTransaction(); List<ProjectionRow> rows = []; List<ProcessedRow> inbox = [];
        using (var q = c.CreateCommand())
        {
            q.Transaction = tx; q.CommandText = "SELECT OrderId,AppliedCount,AppliedAmountCents FROM Projection ORDER BY OrderId COLLATE BINARY";
            using var r = q.ExecuteReader(); while (r.Read()) rows.Add(new(r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
        }
        using (var q = c.CreateCommand())
        {
            q.Transaction = tx; q.CommandText = "SELECT EventId,OrderId,PayloadHash,ProcessedTick FROM ProcessedEvents ORDER BY EventId COLLATE BINARY";
            using var r = q.ExecuteReader(); while (r.Read()) inbox.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3)));
        }
        return new(rows.AsReadOnly(), inbox.AsReadOnly());
    }
}
