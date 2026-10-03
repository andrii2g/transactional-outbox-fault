namespace OutboxFaultLab;

public static class CaseExecutor
{
    public static RunResult Run(Scenario scenario)
    {
        var directory = Path.Combine(Path.GetTempPath(), "outbox-lab-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var factory = new SqliteConnectionFactory(Path.Combine(directory, "producer.db")); SchemaInitializer.Initialize(factory);
            return new ExperimentRunner().Run(scenario, trace => new SqliteStore(factory, trace));
        }
        finally { Directory.Delete(directory, true); }
    }
}
