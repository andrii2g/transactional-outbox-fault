using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OutboxFaultLab;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(Wire.Json); o.Converters.Add(new JsonStringEnumConverter<Component>(JsonNamingPolicy.CamelCase, false)); return o;
    }
    public static void Write<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n", new UTF8Encoding(false));
    public static void WriteCase(string directory, RunResult r)
    {
        Write(Path.Combine(directory, "summary.json"), new
        {
            schemaVersion = 1,
            r.ScenarioId,
            r.Strategy,
            r.ExecutionStatus,
            r.Consistency,
            r.Verification,
            r.Metrics,
            expectedComparisons = r.Comparisons,
            r.Errors,
            r.PlannedFaults,
            r.FiredFaults,
            limitations = new[] { "Exception-based application crashes, not process termination or physical power loss.", "Broker journal survives all modeled application crashes.", "Single producer command and single dispatcher; no producer replay or broker deduplication.", "Logical ticks are not milliseconds." },
            snapshots = r.Snapshots.Select(s => new { s.Phase, s.Tick, s.Epoch, committedOrders = s.Database.Orders.Count, outboxRows = s.Database.Outbox.Count, s.AcceptedJournalCount, s.MissingEvents, s.GhostEvents, s.PendingOutboxRows })
        });
        Write(Path.Combine(directory, "trace.json"), new { schemaVersion = 1, events = r.Trace });
        Write(Path.Combine(directory, "snapshots.json"), new { schemaVersion = 1, snapshots = r.Snapshots, acceptedJournal = r.Deliveries });
    }
}
