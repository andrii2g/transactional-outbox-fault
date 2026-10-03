namespace OutboxFaultLab;

public sealed class DbThenPublishStrategy(ProducerTransaction transaction, IBroker broker) : IStrategy
{ public void Execute(CreateOrder c) { transaction.Execute(c, false); broker.Publish(c.ToEvent(), Component.Producer); } }
