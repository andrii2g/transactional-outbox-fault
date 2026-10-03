namespace OutboxFaultLab;

public sealed class TransactionalOutboxStrategy(ProducerTransaction transaction) : IStrategy
{ public void Execute(CreateOrder c) => transaction.Execute(c, true); }
