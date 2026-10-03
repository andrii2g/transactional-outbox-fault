namespace OutboxFaultLab;

public interface IBroker
{
    void Publish(OrderCreated message, Component component);
}
