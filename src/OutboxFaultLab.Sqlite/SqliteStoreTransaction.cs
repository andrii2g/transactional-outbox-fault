using Microsoft.Data.Sqlite;
using System.Globalization;
namespace OutboxFaultLab;

public sealed class SqliteStoreTransaction : IStoreTransaction
{
    private readonly SqliteConnection connection;
    private readonly SqliteTransaction transaction;
    private readonly TraceSink trace;
    private readonly string id;
    private string? eventId;
    private long mutations;
    private bool committed;
    private bool disposed;
    public SqliteStoreTransaction(SqliteConnectionFactory factory, TraceSink trace, string id)
    {
        connection = factory.Open();
        try { transaction = connection.BeginTransaction(); } catch { connection.Dispose(); throw; }
        this.trace = trace; this.id = id;
    }
    public void InsertOrder(CreateOrder c)
    {
        eventId = c.EventId;
        Execute("order-insert", "INSERT INTO Orders(OrderId,AmountCents) VALUES ($id,$amount)", ("$id", c.OrderId), ("$amount", c.AmountCents));
    }
    public void InsertOutbox(OrderCreated m, string payloadJson, long createdTick)
    {
        eventId = m.EventId;
        Execute("outbox-insert", "INSERT INTO Outbox(EventId,OrderId,PayloadJson,CreatedTick) VALUES ($event,$order,$payload,$tick)",
            ("$event", m.EventId), ("$order", m.OrderId), ("$payload", payloadJson), ("$tick", createdTick));
    }
    public void MarkSent(string eventId, long sentTick)
    {
        this.eventId = eventId;
        Execute("sent-update", "UPDATE Outbox SET SentTick=$tick WHERE EventId=$event AND SentTick IS NULL", ("$tick", sentTick), ("$event", eventId));
    }
    private void Execute(string operation, string sql, params (string Name, object Value)[] values)
    {
        using var q = connection.CreateCommand(); q.Transaction = transaction; q.CommandText = sql;
        foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value);
        if (q.ExecuteNonQuery() != 1) throw new InvalidOperationException("Mutation must affect exactly one row.");
        mutations++; trace.Append("MutationExecuted", eventId: eventId, transactionId: id, details: new Dictionary<string, string> { { "operation", operation } });
    }
    public void Commit()
    {
        transaction.Commit(); committed = true;
        trace.Append("TransactionCommitted", eventId: eventId, transactionId: id,
            details: new Dictionary<string, string> { { "mutations", mutations.ToString(CultureInfo.InvariantCulture) } });
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { transaction.Dispose(); if (!committed) trace.Append("TransactionRolledBack", eventId: eventId, transactionId: id); }
        finally { connection.Dispose(); }
    }
}
