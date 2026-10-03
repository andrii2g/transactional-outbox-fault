namespace OutboxFaultLab;

public interface IFaultInjector
{
    void Reach(FaultContext context);
    int FiredCount { get; }
}
