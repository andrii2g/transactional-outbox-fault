namespace OutboxFaultLab;

public sealed record BrokerPlan(IReadOnlyList<BrokerOutcome> Outcomes, BrokerOutcome DefaultOutcome);
