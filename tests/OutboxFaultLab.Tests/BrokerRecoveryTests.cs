namespace OutboxFaultLab.Tests;

public class BrokerRecoveryTests
{
    public static IEnumerable<object[]> Named() => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "scenarios"), "*.json").Select(p => new object[] { Path.GetFileNameWithoutExtension(p) });
    [Theory]
    [MemberData(nameof(Named))]
    public void NamedCasesMatchTheirExpectations(string name)
    {
        var result = CaseExecutor.Run(ScenarioLoader.Load(ScenarioCatalog.Fixture(name)));
        Assert.True(result.Verification == "passed", string.Join("; ", result.Errors.Concat(result.Comparisons.Where(c => !c.Passed).Select(c => c.Field + ": " + c.Actual + " != " + c.Expected))));
    }
    [Fact]
    public void UnreachedFaultCannotAppearGreen()
    {
        var s = ScenarioLoader.Load(ScenarioCatalog.Fixture("db-first-rejected"));
        s = s with { Faults = [new(Component.Producer, "publish.after-ack", s.Command.EventId, 1)] };
        var r = CaseExecutor.Run(s); Assert.Equal("failed", r.Verification); Assert.Contains(r.Errors, e => e.Contains("not reached", StringComparison.Ordinal));
    }
}
