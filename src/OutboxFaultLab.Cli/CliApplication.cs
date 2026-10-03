namespace OutboxFaultLab;

public sealed class CliApplication
{
    public int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.Help) { output.WriteLine("Outbox Fault Lab: demo | run --scenario FILE | matrix | consumer-demo [--output PATH] [--overwrite] [--help]"); return 0; }
            if (options.Command == "consumer-demo") return ConsumerDemo(options, output);
            var scenarios = options.Command switch { "demo" => ScenarioCatalog.Demo(), "matrix" => ScenarioCatalog.Matrix(), _ => new[] { ScenarioLoader.Load(options.ScenarioPath!) } };
            var multiple = options.Command is "demo" or "matrix";
            var files = scenarios.SelectMany(s => OutputDirectory.CaseFiles.Select(f => multiple ? s.Id + "/" + f : f)).ToList();
            if (multiple) files.AddRange(["matrix.csv", "matrix-summary.json"]);
            var directory = OutputDirectory.Prepare(options.Output, files, options.Overwrite); List<RunResult> results = [];
            foreach (var scenario in scenarios)
            {
                var result = CaseExecutor.Run(scenario); results.Add(result);
                var target = multiple ? Path.Combine(directory, scenario.Id) : directory;
                JsonReportWriter.WriteCase(target, result); CsvReportWriter.Write(Path.Combine(target, "results.csv"), [result]); TimelineSvgWriter.Write(Path.Combine(target, "timeline.svg"), result);
                foreach (var message in result.Errors) error.WriteLine(scenario.Id + ": " + message);
                foreach (var comparison in result.Comparisons.Where(c => !c.Passed)) error.WriteLine(scenario.Id + ": " + comparison.Field + " expected " + comparison.Expected + ", actual " + comparison.Actual);
            }
            if (multiple)
            {
                CsvReportWriter.Write(Path.Combine(directory, "matrix.csv"), results);
                JsonReportWriter.Write(Path.Combine(directory, "matrix-summary.json"), new { schemaVersion = 1, cases = results.Count, passed = results.Count(r => r.Verification == "passed"), failed = results.Count(r => r.Verification == "failed"), results = results.Select(r => new { r.ScenarioId, r.Strategy, r.ExecutionStatus, r.Verification, r.Metrics }) });
            }
            ConsoleSummaryWriter.Write(output, results); output.WriteLine("Reports: " + options.Output);
            if (results.Any(r => r.Verification == "failed")) return 1;
            return results.Zip(scenarios).Any(p => p.First.ExecutionStatus == "unresolved" && (!p.Second.Expected.TryGetValue("executionStatus", out var expected) || expected.GetString() != "unresolved")) ? 3 : 0;
        }
        catch (Exception e) when (e is CliUsageException or ScenarioValidationException or OutputPathException) { error.WriteLine(e.Message); return 2; }
        catch (Exception e) { error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }
    }
    private static int ConsumerDemo(CliOptions options, TextWriter output)
    {
        var files = new[] { "consumer-summary.json", "naive-timeline.svg", "idempotent-timeline.svg" };
        var directory = OutputDirectory.Prepare(options.Output, files, options.Overwrite);
        var producer = CaseExecutor.Run(ScenarioLoader.Load(ScenarioCatalog.Fixture("outbox-after-accept")));
        var temporary = Path.Combine(Path.GetTempPath(), "outbox-consumer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        try
        {
            var results = new[] { false, true }.Select(idempotent =>
            {
                var factory = new SqliteConnectionFactory(Path.Combine(temporary, idempotent ? "idempotent.db" : "naive.db")); SchemaInitializer.Initialize(factory, true);
                return new ConsumerRunner().Run(producer.Deliveries, idempotent, t => new SqliteConsumerStore(factory, t));
            }).ToArray();
            var passed = producer.Verification == "passed" && results[0].Final.Projection.Single().AppliedCount == 2 && results[1].Final.Projection.Single().AppliedCount == 1;
            JsonReportWriter.Write(Path.Combine(directory, "consumer-summary.json"), new { schemaVersion = 1, verification = passed ? "passed" : "failed", sourceScenario = producer.ScenarioId, acceptedJournal = producer.Deliveries, consumers = results, limitation = "Deduplicated effects apply inside the consumer SQLite transaction only; external side effects are outside this guarantee." });
            foreach (var r in results) { TimelineSvgWriter.WriteConsumer(Path.Combine(directory, r.Mode + "-timeline.svg"), r); output.WriteLine($"{r.Mode}: applied={r.Final.Projection.Sum(p => p.AppliedCount)}, skipped={r.DuplicateSkipped}"); }
            output.WriteLine("Reports: " + options.Output); return passed ? 0 : 1;
        }
        finally { Directory.Delete(temporary, true); }
    }
}
