namespace OutboxFaultLab;

public sealed record TimingSpec
{
    public long CommitTicks { get; init; }
    public long PublishTicks { get; init; }
    public long PollTicks { get; init; }
    public long RestartTicks { get; init; }
    public long RetryBaseTicks { get; init; }
    public long RetryCapTicks { get; init; }
    public TimingSpec(long commitTicks, long publishTicks, long pollTicks, long restartTicks, long retryBaseTicks, long retryCapTicks)
    {
        foreach (var n in new[] { commitTicks, publishTicks, pollTicks, restartTicks, retryBaseTicks, retryCapTicks }) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        if (retryCapTicks < retryBaseTicks) throw new ArgumentOutOfRangeException(nameof(retryCapTicks));
        CommitTicks = commitTicks; PublishTicks = publishTicks; PollTicks = pollTicks; RestartTicks = restartTicks; RetryBaseTicks = retryBaseTicks; RetryCapTicks = retryCapTicks;
    }
}
