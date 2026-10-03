using System.Text.Json;
namespace OutboxFaultLab;

public static class RunVerifier
{
    public static (IReadOnlyList<Comparison> Comparisons, IReadOnlyList<string> Errors) Verify(Scenario s,
        DatabaseSnapshot db, IReadOnlyList<BrokerDelivery> deliveries, Metrics metrics, string status, int fired)
    {
        List<string> errors = []; List<Comparison> comparisons = [];
        if (fired != s.Faults.Count) errors.Add("Scheduled fault not reached: " + fired + "/" + s.Faults.Count);
        var expectedEvent = s.Command.ToEvent();
        foreach (var o in db.Orders) if (o.OrderId != s.Command.OrderId || o.AmountCents != s.Command.AmountCents) errors.Add("Unexpected committed order identity or amount.");
        foreach (var d in deliveries) if (d.Event != expectedEvent) errors.Add("Accepted payload differs from scenario identity.");
        foreach (var row in db.Outbox)
        {
            if (!db.Orders.Any(o => o.OrderId == row.OrderId)) errors.Add("Outbox has no corresponding order.");
            if (JsonSerializer.Deserialize<OrderCreated>(row.PayloadJson, Wire.Json) != expectedEvent || row.EventId != expectedEvent.EventId || row.OrderId != expectedEvent.OrderId)
                errors.Add("Outbox payload identity mismatch.");
            if (row.SentTick is not null && !deliveries.Any(d => d.Event.EventId == row.EventId)) errors.Add("Sent marker ahead of acceptance.");
        }
        if (s.Strategy == StrategyKind.Outbox && (db.Outbox.Count != db.Orders.Count || metrics.LostEvents != 0 || metrics.GhostEvents != 0)) errors.Add("Outbox invariant violated.");
        var actual = JsonSerializer.SerializeToElement(metrics, Wire.Json);
        foreach (var (field, value) in s.Expected.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var a = field == "executionStatus" ? JsonSerializer.SerializeToElement(status) : actual.GetProperty(field);
            var pass = JsonElement.DeepEquals(a, value);
            comparisons.Add(new(field, value.GetRawText(), a.GetRawText(), pass));
        }
        return (comparisons.AsReadOnly(), errors.AsReadOnly());
    }
}
