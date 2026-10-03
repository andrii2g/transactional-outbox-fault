namespace OutboxFaultLab;

public sealed class BudgetExhaustedException : Exception;
public sealed class LogicalClock(long maxTicks = long.MaxValue) : ILogicalClock
{
    public long Tick { get; private set; }
    public void Advance(long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        var next = checked(Tick + ticks);
        if (next > maxTicks) throw new BudgetExhaustedException();
        Tick = next;
    }
}
