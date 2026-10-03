namespace OutboxFaultLab;

public sealed record OutboxRow(long Sequence, string EventId, string OrderId,
    string PayloadJson, long CreatedTick, long? SentTick);
