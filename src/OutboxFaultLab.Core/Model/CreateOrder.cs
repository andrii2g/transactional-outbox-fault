using System.Text.RegularExpressions;
namespace OutboxFaultLab;

public sealed record CreateOrder
{
    public string OrderId { get; }
    public string EventId { get; }
    public long AmountCents { get; }
    public CreateOrder(string orderId, string eventId, long amountCents)
    {
        ValidateId(orderId); ValidateId(eventId);
        ArgumentOutOfRangeException.ThrowIfNegative(amountCents);
        OrderId = orderId; EventId = eventId; AmountCents = amountCents;
    }
    public static void ValidateId(string id)
    {
        if (id is null || !Regex.IsMatch(id, "\\A[A-Za-z0-9_-]{1,64}\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Identifier must contain 1–64 ASCII letters, digits, underscores or hyphens.");
    }
    public OrderCreated ToEvent() => new(1, EventId, OrderId, AmountCents);
}
