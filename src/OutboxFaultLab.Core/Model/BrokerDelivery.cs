namespace OutboxFaultLab;

public sealed record BrokerDelivery(long Sequence, OrderCreated Event, long AcceptedTick, int Epoch);
