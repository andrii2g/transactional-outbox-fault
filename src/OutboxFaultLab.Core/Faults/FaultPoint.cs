namespace OutboxFaultLab;

public static class FaultPoint
{
    public static readonly string[] Inserts = ["order-insert.before", "order-insert.after", "producer-commit.before", "producer-commit.after"];
    public static readonly string[] Publish = ["publish.before", "publish.after-accept", "publish.after-ack"];
    public static readonly string[] Outbox = ["outbox-insert.before", "outbox-insert.after"];
    public static readonly string[] Sent = ["sent-update.before", "sent-update.after", "sent-commit.before", "sent-commit.after"];
    public static IEnumerable<string> Allowed(StrategyKind s, Component c) => (s, c) switch
    {
        (StrategyKind.Outbox, Component.Producer) => Inserts.Concat(Outbox),
        (StrategyKind.Outbox, Component.Dispatcher) => Publish.Concat(Sent),
        (_, Component.Producer) => Inserts.Concat(Publish),
        _ => []
    };
}
