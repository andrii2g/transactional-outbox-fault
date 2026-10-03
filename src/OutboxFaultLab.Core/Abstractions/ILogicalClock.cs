namespace OutboxFaultLab;

public interface ILogicalClock
{
    long Tick { get; }
    void Advance(long ticks);
}
