namespace OutboxFaultLab;

public interface IStrategy
{
    void Execute(CreateOrder command);
}
