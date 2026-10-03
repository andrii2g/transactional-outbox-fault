namespace OutboxFaultLab;

public sealed class CliUsageException(string message) : Exception(message);
public sealed record CliOptions(string Command, string? ScenarioPath, string Output, bool Overwrite, bool Help)
{
    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0 || args is ["--help"]) return new("demo", null, "artifacts/demo", false, true);
        var command = args[0]; if (command is not ("demo" or "run" or "matrix" or "consumer-demo")) throw new CliUsageException("Unknown command: " + command);
        string? scenario = null; var output = "artifacts/" + command; var overwrite = false; var help = false; var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i]; if (!seen.Add(option)) throw new CliUsageException("Duplicate option: " + option);
            switch (option)
            {
                case "--help": help = true; break;
                case "--overwrite": overwrite = true; break;
                case "--scenario": scenario = Value(); break;
                case "--output": output = Value(); break;
                default: throw new CliUsageException("Unknown option: " + option);
            }
            string Value() { if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[i])) throw new CliUsageException("Missing value for " + option); return args[i]; }
        }
        if (!help && (command == "run" ? scenario is null : scenario is not null)) throw new CliUsageException("--scenario is required only for run.");
        return new(command, scenario, output, overwrite, help);
    }
}
