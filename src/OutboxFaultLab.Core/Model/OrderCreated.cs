namespace OutboxFaultLab;

public sealed record OrderCreated
{
    public int Version { get; }
    public string EventId { get; }
    public string OrderId { get; }
    public long AmountCents { get; }
    public OrderCreated(int version, string eventId, string orderId, long amountCents)
    {
        if (version != 1) throw new ArgumentOutOfRangeException(nameof(version));
        CreateOrder.ValidateId(eventId); CreateOrder.ValidateId(orderId); ArgumentOutOfRangeException.ThrowIfNegative(amountCents);
        Version = version; EventId = eventId; OrderId = orderId; AmountCents = amountCents;
    }
}
