using Microsoft.Data.Sqlite;
namespace OutboxFaultLab.Tests;

public class TransactionAtomicityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalRollsBackAndCommitSurvivesReopen(bool commit)
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(); var trace = new TraceSink(new LogicalClock()); var store = new SqliteStore(f, trace);
        var command = new CreateOrder("O", "E", 12);
        using (var tx = store.BeginTransaction("producer-tx-1")) { tx.InsertOrder(command); tx.InsertOutbox(command.ToEvent(), Wire.Payload(command.ToEvent()), 0); if (commit) tx.Commit(); }
        var db = new SqliteStore(new(temp.FilePath()), trace).ReadCommittedSnapshot(); Assert.Equal(commit ? 1 : 0, db.Orders.Count); Assert.Equal(db.Orders.Count, db.Outbox.Count);
        using var c = f.Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA foreign_keys"; Assert.Equal(1L, q.ExecuteScalar());
        q.CommandText = "PRAGMA integrity_check"; Assert.Equal("ok", q.ExecuteScalar());
    }
    [Fact]
    public void ConstraintsAndMarkerAffectedRowChecksAreReal()
    {
        using var temp = new TempDatabase(); var f = temp.Initialize(); var store = new SqliteStore(f, new(new LogicalClock())); var command = new CreateOrder("O", "E", 12);
        using (var tx = store.BeginTransaction("p")) { Assert.Throws<SqliteException>(() => tx.InsertOutbox(command.ToEvent(), Wire.Payload(command.ToEvent()), 0)); }
        using (var tx = store.BeginTransaction("p")) { tx.InsertOrder(command); tx.InsertOutbox(command.ToEvent(), Wire.Payload(command.ToEvent()), 0); Assert.Throws<SqliteException>(() => tx.InsertOutbox(command.ToEvent(), Wire.Payload(command.ToEvent()), 0)); tx.Commit(); }
        using (var tx = store.BeginTransaction("s")) { tx.MarkSent("E", 1); }
        Assert.Null(store.ReadOldestUnsent()!.SentTick);
        using (var tx = store.BeginTransaction("s")) { tx.MarkSent("E", 1); tx.Commit(); }
        using (var tx = store.BeginTransaction("s")) { Assert.Throws<InvalidOperationException>(() => tx.MarkSent("E", 2)); }
    }
}
