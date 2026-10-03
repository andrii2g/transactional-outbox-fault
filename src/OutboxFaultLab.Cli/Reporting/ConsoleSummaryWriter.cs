namespace OutboxFaultLab;

public static class ConsoleSummaryWriter
{
    public static void Write(TextWriter output, IEnumerable<RunResult> results)
    {
        output.WriteLine("Case | orders | accepted | lost | ghost | pending | duplicates | status | verification");
        foreach (var r in results) output.WriteLine($"{r.ScenarioId} | {r.Metrics.CommittedOrders} | {r.Metrics.AcceptedDeliveries} | {r.Metrics.LostEvents} | {r.Metrics.GhostEvents} | {r.Metrics.PendingOutboxRows} | {r.Metrics.DuplicateDeliveries} | {r.ExecutionStatus} | {r.Verification}");
    }
}
