namespace OutboxFaultLab;

public static class RetryPolicy
{
    public static long Delay(TimingSpec timing, int failures)
    {
        var backoff = timing.RetryBaseTicks;
        for (var i = 1; i < failures && backoff < timing.RetryCapTicks; i++)
            backoff = backoff > timing.RetryCapTicks / 2 ? timing.RetryCapTicks : backoff * 2;
        return Math.Max(timing.PollTicks, Math.Min(timing.RetryCapTicks, backoff));
    }
}
