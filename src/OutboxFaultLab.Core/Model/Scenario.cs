using System.Text.Json;
namespace OutboxFaultLab;

public sealed record Scenario(string Id, StrategyKind Strategy, CreateOrder Command, TimingSpec Timing,
    BudgetSpec Budgets, IReadOnlyList<FaultSpec> Faults, BrokerPlan Broker, IReadOnlyDictionary<string, JsonElement> Expected);
public static class Wire
{
    public static string Strategy(StrategyKind s) => s switch { StrategyKind.DbThenPublish => "db-first", StrategyKind.PublishThenDb => "publish-first", _ => "outbox" };
    public static string Component(Component c) => c.ToString().ToLowerInvariant();
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static string Payload(OrderCreated e) => JsonSerializer.Serialize(e, Json);
}
