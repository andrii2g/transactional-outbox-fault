namespace OutboxFaultLab;

public interface IStore
{
    IStoreTransaction BeginTransaction(string transactionId);
    OutboxRow? ReadOldestUnsent();
    DatabaseSnapshot ReadCommittedSnapshot();
}
