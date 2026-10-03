using System.Globalization;
namespace OutboxFaultLab;

public sealed class FaultInjector(IReadOnlyList<FaultSpec> plan, ITraceSink trace) : IFaultInjector
{
    private readonly Dictionary<(Component, string, string), int> counts = [];
    private readonly HashSet<FaultSpec> fired = [];
    public int FiredCount => fired.Count;
    public void Reach(FaultContext c)
    {
        var key = (c.Component, c.Point, c.EventId); counts.TryGetValue(key, out var n); counts[key] = ++n;
        trace.Append("HookReached", c.Component, c.Point, c.EventId, c.TransactionId, new Dictionary<string, string> { { "occurrence", n.ToString(CultureInfo.InvariantCulture) } });
        var hit = plan.FirstOrDefault(f => f.Component == c.Component && f.Point == c.Point && f.EventId == c.EventId && f.Occurrence == n && !fired.Contains(f));
        if (hit is null) return;
        fired.Add(hit); trace.Append("CrashInjected", c.Component, c.Point, c.EventId, c.TransactionId);
        throw new SimulatedCrashException(c);
    }
}
