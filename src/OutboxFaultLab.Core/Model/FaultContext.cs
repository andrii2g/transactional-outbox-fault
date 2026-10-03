namespace OutboxFaultLab;

public sealed record FaultContext(Component Component, string Point, string EventId,
    long Tick, int Epoch, string? TransactionId);
