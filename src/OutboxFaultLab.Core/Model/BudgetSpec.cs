namespace OutboxFaultLab;

public sealed record BudgetSpec
{
    public int MaxRestarts { get; init; }
    public int MaxPublishAttempts { get; init; }
    public long MaxTicks { get; init; }
    public BudgetSpec(int maxRestarts, int maxPublishAttempts, long maxTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRestarts); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPublishAttempts); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicks);
        MaxRestarts = maxRestarts; MaxPublishAttempts = maxPublishAttempts; MaxTicks = maxTicks;
    }
}
