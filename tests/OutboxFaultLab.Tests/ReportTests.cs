using System.Text.Json;
using System.Xml.Linq;
namespace OutboxFaultLab.Tests;

public class ReportTests
{
    [Fact]
    public void TypedJsonAndCsvMatchRuntimeAndSvgParses()
    {
        using var temp = new TempDatabase(); var result = CaseExecutor.Run(ScenarioLoader.Load(ScenarioCatalog.Fixture("outbox-after-accept")));
        JsonReportWriter.WriteCase(temp.DirectoryPath, result); var csv = temp.FilePath("results.csv"); CsvReportWriter.Write(csv, [result]);
        using var summary = JsonDocument.Parse(File.ReadAllText(temp.FilePath("summary.json")));
        Assert.Equal(result.Metrics.AcceptedDeliveries, summary.RootElement.GetProperty("metrics").GetProperty("acceptedDeliveries").GetInt64());
        var lines = File.ReadAllLines(csv); var cells = lines[1].Split(','); Assert.Equal(CsvReportWriter.Columns.Length, cells.Length);
        for (var i = 1; i < CsvReportWriter.Columns.Length; i++)
        {
            var column = CsvReportWriter.Columns[i]; var metrics = summary.RootElement.GetProperty("metrics");
            if (metrics.TryGetProperty(column, out var actual)) Assert.Equal(actual.ValueKind == JsonValueKind.Null ? "" : actual.GetRawText(), cells[i]);
        }
        TimelineSvgWriter.Write(temp.FilePath("timeline.svg"), result);
        var svg = XDocument.Load(temp.FilePath("timeline.svg")); Assert.Equal("http://www.w3.org/2000/svg", svg.Root!.Name.NamespaceName);
        Assert.NotNull(svg.Root.Element(svg.Root.Name.Namespace + "title")); Assert.Contains("DUPLICATE", svg.ToString(), StringComparison.Ordinal);
        Assert.Equal("1280", svg.Root.Attribute("viewBox")!.Value.Split(' ')[2]);
        Assert.Equal("\"a,\"\"b\"", CsvReportWriter.Encode("a,\"b"));
    }
    [Fact]
    public void SvgEscapesUntrustedLabelsAndRetainsAllTraceEvents()
    {
        using var temp = new TempDatabase(); var r = CaseExecutor.Run(ScenarioLoader.Load(ScenarioCatalog.Fixture("outbox-crash-twice")));
        r = r with { ScenarioId = "<script>&\"", Trace = r.Trace.Select(t => t with { Point = "<tag>&" }).ToArray() };
        TimelineSvgWriter.Write(temp.FilePath("svg.svg"), r); var svg = XDocument.Load(temp.FilePath("svg.svg"));
        Assert.DoesNotContain(svg.Descendants(), e => e.Name.LocalName == "script"); Assert.Equal(r.ScenarioId, svg.Root!.Element(svg.Root.Name.Namespace + "title")!.Value);
        foreach (var e in r.Trace) Assert.Contains("#" + e.Sequence + "  ", svg.ToString(), StringComparison.Ordinal);
    }
}
