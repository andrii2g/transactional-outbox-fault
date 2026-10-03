namespace OutboxFaultLab.Tests;

public class StrategyFailureMatrixTests
{
    public static IEnumerable<object[]> Matrix() => ScenarioCatalog.Matrix().Select(s => new object[] { s.Id });
    [Theory]
    [MemberData(nameof(Matrix))]
    public void ActualSqliteRunMatchesIndependentCsv(string id)
    {
        var s = ScenarioCatalog.Matrix().Single(s => s.Id == id); var result = CaseExecutor.Run(s);
        Assert.True(result.Verification == "passed", string.Join("; ", result.Errors.Concat(result.Comparisons.Where(c => !c.Passed).Select(c => c.Field + ": " + c.Actual + " != " + c.Expected))));
        Assert.Equal(s.Faults.Count, result.FiredFaults);
    }
    [Fact]
    public void InventoryMatchesEveryCatalogBoundary()
    {
        var cases = ScenarioCatalog.Matrix(); Assert.Equal(30, cases.Count); Assert.Equal(27, cases.Sum(s => s.Faults.Count));
        foreach (var kind in Enum.GetValues<StrategyKind>())
            foreach (var component in new[] { Component.Producer, Component.Dispatcher })
                foreach (var point in FaultPoint.Allowed(kind, component))
                    Assert.Single(cases, s => s.Strategy == kind && s.Faults.Any(f => f.Component == component && f.Point == point));
    }
}
