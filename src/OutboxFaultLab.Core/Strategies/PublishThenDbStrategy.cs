namespace OutboxFaultLab;

public sealed class PublishThenDbStrategy(ProducerTransaction transaction, IBroker broker) : IStrategy
{ public void Execute(CreateOrder c) { broker.Publish(c.ToEvent(), Component.Producer); transaction.Execute(c, false); } }
