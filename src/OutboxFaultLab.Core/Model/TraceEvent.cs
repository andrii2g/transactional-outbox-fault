namespace OutboxFaultLab;

public sealed record TraceEvent(long Sequence, long Tick, int Epoch, string Kind,
    Component? Component, string? Point, string? EventId, string? TransactionId,
    IReadOnlyDictionary<string, string> Details);
