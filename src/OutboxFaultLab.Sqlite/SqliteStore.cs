using Microsoft.Data.Sqlite;
namespace OutboxFaultLab;

public sealed class SqliteStore(SqliteConnectionFactory factory, TraceSink trace) : IStore
{
    public IStoreTransaction BeginTransaction(string id) => new SqliteStoreTransaction(factory, trace, id);
    public OutboxRow? ReadOldestUnsent()
    {
        using var c = factory.Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT Sequence,EventId,OrderId,PayloadJson,CreatedTick,SentTick FROM Outbox WHERE SentTick IS NULL ORDER BY Sequence LIMIT 1";
        using var r = q.ExecuteReader(); return r.Read() ? Row(r) : null;
    }
    public DatabaseSnapshot ReadCommittedSnapshot()
    {
        using var c = factory.Open(); using var tx = c.BeginTransaction();
        List<OrderRow> orders = []; List<OutboxRow> rows = [];
        using (var q = c.CreateCommand())
        {
            q.Transaction = tx; q.CommandText = "SELECT OrderId,AmountCents FROM Orders ORDER BY OrderId COLLATE BINARY";
            using var r = q.ExecuteReader(); while (r.Read()) orders.Add(new(r.GetString(0), r.GetInt64(1)));
        }
        using (var q = c.CreateCommand())
        {
            q.Transaction = tx; q.CommandText = "SELECT Sequence,EventId,OrderId,PayloadJson,CreatedTick,SentTick FROM Outbox ORDER BY Sequence";
            using var r = q.ExecuteReader(); while (r.Read()) rows.Add(Row(r));
        }
        return new(orders.AsReadOnly(), rows.AsReadOnly());
    }
    private static OutboxRow Row(SqliteDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.IsDBNull(5) ? null : r.GetInt64(5));
}
