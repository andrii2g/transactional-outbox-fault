using System.Text.Json;
using System.Text.RegularExpressions;
namespace OutboxFaultLab;

public sealed class ScenarioValidationException(string message) : Exception(message);
public static class ScenarioLoader
{
    public static Scenario Load(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", "scenario.schema.json")));
            Validate(doc.RootElement, schema.RootElement, "scenario");
            var r = doc.RootElement; var c = r.GetProperty("command"); var t = r.GetProperty("timing"); var b = r.GetProperty("budgets"); var p = r.GetProperty("broker");
            var strategy = r.GetProperty("strategy").GetString() switch { "db-first" => StrategyKind.DbThenPublish, "publish-first" => StrategyKind.PublishThenDb, _ => StrategyKind.Outbox };
            var command = new CreateOrder(c.GetProperty("orderId").GetString()!, c.GetProperty("eventId").GetString()!, c.GetProperty("amountCents").GetInt64());
            var timing = new TimingSpec(N(t, "commitTicks"), N(t, "publishTicks"), N(t, "pollTicks"), N(t, "restartTicks"), N(t, "retryBaseTicks"), N(t, "retryCapTicks"));
            if (timing.RetryCapTicks < timing.RetryBaseTicks) throw new ScenarioValidationException("retryCapTicks must be at least retryBaseTicks.");
            var faults = r.GetProperty("faults").EnumerateArray().Select(f => new FaultSpec(f.GetProperty("component").GetString() == "producer" ? Component.Producer : Component.Dispatcher,
                f.GetProperty("point").GetString()!, f.GetProperty("eventId").GetString()!, f.GetProperty("occurrence").GetInt32())).ToArray();
            foreach (var fault in faults)
                if (fault.EventId != command.EventId || !FaultPoint.Allowed(strategy, fault.Component).Contains(fault.Point, StringComparer.Ordinal))
                    throw new ScenarioValidationException("Fault does not match command or strategy catalog.");
            if (faults.Distinct().Count() != faults.Length) throw new ScenarioValidationException("Duplicate fault schedule entry.");
            return new(r.GetProperty("id").GetString()!, strategy, command, timing,
                new(b.GetProperty("maxRestarts").GetInt32(), b.GetProperty("maxPublishAttempts").GetInt32(), N(b, "maxTicks")), faults,
                new(p.GetProperty("outcomes").EnumerateArray().Select(e => Outcome(e.GetString()!)).ToArray(), Outcome(p.GetProperty("defaultOutcome").GetString()!)),
                r.GetProperty("expected").EnumerateObject().ToDictionary(e => e.Name, e => e.Value.Clone(), StringComparer.Ordinal));
        }
        catch (ArgumentException e) { throw new ScenarioValidationException("Invalid scenario: " + e.Message); }
        catch (JsonException e) { throw new ScenarioValidationException("Invalid JSON: " + e.Message); }
        catch (IOException e) { throw new ScenarioValidationException("Cannot read scenario: " + e.Message); }
        catch (UnauthorizedAccessException e) { throw new ScenarioValidationException("Cannot read scenario: " + e.Message); }
    }
    private static long N(JsonElement e, string name) => e.GetProperty(name).GetInt64();
    private static BrokerOutcome Outcome(string v) => v switch { "reject" => BrokerOutcome.Reject, "accept-ack-lost" => BrokerOutcome.AcceptAckLost, _ => BrokerOutcome.AcceptAck };
    private static void Validate(JsonElement value, JsonElement schema, string path)
    {
        void Fail(string reason) => throw new ScenarioValidationException(path + ": " + reason);
        var type = value.ValueKind switch { JsonValueKind.Object => "object", JsonValueKind.Array => "array", JsonValueKind.String => "string", JsonValueKind.Number => "integer", JsonValueKind.Null => "null", _ => "invalid" };
        if (schema.TryGetProperty("type", out var expected) && (expected.ValueKind == JsonValueKind.Array ? !expected.EnumerateArray().Any(e => e.GetString() == type) : expected.GetString() != type)) Fail("wrong type");
        if (type == "integer" && !value.TryGetInt64(out _)) Fail("requires an Int64 integer");
        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, value)) Fail("wrong constant");
        if (schema.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(e => JsonElement.DeepEquals(e, value))) Fail("unknown enum value");
        if (type == "integer")
        {
            var n = value.GetInt64();
            if (schema.TryGetProperty("minimum", out var min) && n < min.GetInt64()) Fail("below minimum");
            if (schema.TryGetProperty("maximum", out var max) && n > max.GetInt64()) Fail("above maximum");
        }
        if (type == "string" && schema.TryGetProperty("pattern", out var pattern))
        {
            var patternText = pattern.GetString()!.Replace("^", "\\A", StringComparison.Ordinal).Replace("$", "\\z", StringComparison.Ordinal);
            if (!Regex.IsMatch(value.GetString()!, patternText, RegexOptions.CultureInvariant)) Fail("invalid identifier");
        }
        if (type == "array")
        {
            if (schema.TryGetProperty("maxItems", out var max) && value.GetArrayLength() > max.GetInt32()) Fail("too many items");
            var index = 0; foreach (var item in value.EnumerateArray()) Validate(item, schema.GetProperty("items"), path + "[" + (index++) + "]");
        }
        if (type == "object")
        {
            var properties = schema.GetProperty("properties"); var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) Fail("duplicate JSON property " + property.Name);
                if (!properties.TryGetProperty(property.Name, out var spec)) Fail("unknown property " + property.Name);
                Validate(property.Value, spec, path + "." + property.Name);
            }
            foreach (var name in schema.GetProperty("required").EnumerateArray()) if (!names.Contains(name.GetString()!)) Fail("missing " + name.GetString());
        }
    }
}
