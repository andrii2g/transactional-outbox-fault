using System.Text.Json;
namespace OutboxFaultLab;

public static class ScenarioCatalog
{
    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "scenarios", name + ".json");
    public static IReadOnlyList<Scenario> Demo() => new[] { "db-first-lost", "publish-first-ghost", "outbox-before-dispatch", "outbox-after-accept" }.Select(n => ScenarioLoader.Load(Fixture(n))).ToArray();
    public static IReadOnlyList<Scenario> Matrix()
    {
        var paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios", "matrix"), "*.json").Order(StringComparer.Ordinal);
        var cases = paths.Select(ScenarioLoader.Load).ToArray();
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "expected", "single-crash-matrix.csv"));
        var header = lines[0].Split(','); var expectations = new Dictionary<string, IReadOnlyDictionary<string, JsonElement>>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(','); if (cells.Length != header.Length) throw new ScenarioValidationException("Malformed expected matrix.");
            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            for (var i = 1; i < header.Length; i++) fields[header[i]] = JsonSerializer.SerializeToElement(header[i] == "executionStatus" ? (object)cells[i] : long.Parse(cells[i], System.Globalization.CultureInfo.InvariantCulture));
            if (!expectations.TryAdd(cells[0], fields)) throw new ScenarioValidationException("Duplicate expected matrix ID.");
        }
        if (cases.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != cases.Length || !expectations.Keys.Order(StringComparer.Ordinal).SequenceEqual(cases.Select(s => s.Id).Order(StringComparer.Ordinal)))
            throw new ScenarioValidationException("Matrix IDs and expectations do not match one-to-one.");
        return cases.Select(s => s with { Expected = expectations[s.Id] }).ToArray();
    }
}
