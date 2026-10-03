using System.Globalization;
using System.Text;
using System.Text.Json;
namespace OutboxFaultLab;

public static class CsvReportWriter
{
    public static readonly string[] Columns = ["id", "committedOrders", "acceptedDeliveries", "duplicateDeliveries", "lostEvents", "ghostEvents", "pendingOutboxRows", "publishAttempts", "publishRetries", "committedRowMutations", "extraWrites", "transactionCommits", "restartCount", "executionStatus", "distinctAcceptedEvents", "missingEvents", "recoverablePendingEvents", "attemptedRowMutations", "deliveryLatencyTicks", "recoveryLatencyTicks", "recoveryDrainTicks", "strategy", "consistency", "verification"];
    public static string Encode(string field) => field.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : field;
    public static void Write(string path, IEnumerable<RunResult> results)
    {
        var lines = new List<string> { string.Join(',', Columns) };
        foreach (var r in results)
        {
            var m = JsonSerializer.SerializeToElement(r.Metrics, Wire.Json);
            var values = Columns.Select(c => c switch
            {
                "id" => r.ScenarioId,
                "strategy" => r.Strategy,
                "executionStatus" => r.ExecutionStatus,
                "consistency" => r.Consistency,
                "verification" => r.Verification,
                _ => m.GetProperty(c).ValueKind == JsonValueKind.Null ? "" : m.GetProperty(c).GetInt64().ToString(CultureInfo.InvariantCulture)
            });
            lines.Add(string.Join(',', values.Select(Encode)));
        }
        File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
    }
}
