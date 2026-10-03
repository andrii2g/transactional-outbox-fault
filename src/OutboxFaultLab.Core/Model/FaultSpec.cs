namespace OutboxFaultLab;

public sealed record FaultSpec(Component Component, string Point, string EventId, int Occurrence);
