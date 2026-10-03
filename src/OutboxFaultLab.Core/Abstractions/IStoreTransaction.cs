namespace OutboxFaultLab;

public interface IStoreTransaction : IDisposable
{
    void InsertOrder(CreateOrder command);
    void InsertOutbox(OrderCreated message, string payloadJson, long createdTick);
    void MarkSent(string eventId, long sentTick);
    void Commit();
}
