namespace OutboxFaultLab;

public sealed record DatabaseSnapshot(IReadOnlyList<OrderRow> Orders, IReadOnlyList<OutboxRow> Outbox);
