namespace OutboxFaultLab;

public sealed class RuntimeFactory(Func<IStore> stores, IBroker broker, LogicalClock clock,
    TraceSink trace, IFaultInjector faults, TimingSpec timing, TransactionIds ids)
{
    public IStrategy Producer(StrategyKind kind)
    {
        var tx = new ProducerTransaction(stores(), clock, trace, faults, timing);
        return kind switch
        {
            StrategyKind.DbThenPublish => new DbThenPublishStrategy(tx, broker),
            StrategyKind.PublishThenDb => new PublishThenDbStrategy(tx, broker),
            _ => new TransactionalOutboxStrategy(tx)
        };
    }
    public OutboxDispatcher Dispatcher() => new(stores(), broker, clock, trace, faults, timing, ids);
}
