namespace OutboxFaultLab;

public sealed class TraceSink(ILogicalClock clock) : ITraceSink
{
    private readonly List<TraceEvent> events = [];
    public int Epoch { get; set; }
    public IReadOnlyList<TraceEvent> Events => events.AsReadOnly();
    public void Append(string kind, Component? component = null, string? point = null, string? eventId = null,
        string? transactionId = null, IReadOnlyDictionary<string, string>? details = null)
        => events.Add(new(checked(events.Count + 1L), clock.Tick, Epoch, kind, component, point, eventId, transactionId,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(details is null ? new Dictionary<string, string>() : new Dictionary<string, string>(details, StringComparer.Ordinal))));
}
