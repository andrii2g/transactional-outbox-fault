using System.Text.Json.Nodes;
namespace OutboxFaultLab.Tests;

public class CliTests
{
    private static int Run(params string[] args) => new CliApplication().Run(args, new StringWriter(), new StringWriter());
    [Theory][InlineData("--help", 0)][InlineData("unknown", 2)][InlineData("run", 2)] public void BasicExitCodes(string command, int code) => Assert.Equal(code, Run(command));
    [Fact]
    public void OptionsRejectUnknownDuplicateAndIncompatibleArguments()
    {
        Assert.Equal(2, Run("demo", "--wat")); Assert.Equal(2, Run("demo", "--output", "a", "--output", "b"));
        Assert.Equal(2, Run("demo", "--scenario", "x")); Assert.Equal(2, Run("run", "--scenario"));
    }
    [Fact]
    public void UnownedOutputAndUnrelatedFilesArePreserved()
    {
        using var t = new TempDatabase(); var path = Path.Combine(t.DirectoryPath, "output"); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "user.txt"), "preserve"); Assert.Equal(2, Run("demo", "--output", path, "--overwrite"));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(path, "user.txt")));
        var owned = Path.Combine(t.DirectoryPath, "owned"); Assert.Equal(0, Run("demo", "--output", owned));
        File.WriteAllText(Path.Combine(owned, "user.txt"), "preserve");
        Assert.Equal(2, Run("demo", "--output", owned)); Assert.Equal(0, Run("demo", "--output", owned, "--overwrite"));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(owned, "user.txt")));
    }
    [Fact]
    public void ScenarioValidationRejectsMalformedMissingDuplicateAndUnsafeData()
    {
        using var temp = new TempDatabase(); var path = temp.FilePath("scenario.json");
        var original = File.ReadAllText(ScenarioCatalog.Fixture("outbox-after-accept"));
        var invalid = new[] { "{", original.Replace("outbox-after-accept", "../unsafe", StringComparison.Ordinal), original.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1", StringComparison.Ordinal), original.Replace("\"amountCents\": 1250", "\"amountCents\": -1", StringComparison.Ordinal) };
        foreach (var text in invalid) { File.WriteAllText(path, text); Assert.Equal(2, Run("run", "--scenario", path, "--output", Path.Combine(temp.DirectoryPath, "output"))); }
        var node = JsonNode.Parse(original)!.AsObject(); node.Remove("timing"); File.WriteAllText(path, node.ToJsonString());
        Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Load(path));
        node = JsonNode.Parse(original)!.AsObject(); node["unexpected"] = 1; File.WriteAllText(path, node.ToJsonString()); Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Load(path));
        node = JsonNode.Parse(original)!.AsObject(); node["timing"]!["retryCapTicks"] = 1; File.WriteAllText(path, node.ToJsonString()); Assert.Throws<ScenarioValidationException>(() => ScenarioLoader.Load(path));
    }
    [Fact]
    public void UnresolvedAndVerificationFailuresHaveDocumentedExitCodes()
    {
        using var temp = new TempDatabase(); var source = ScenarioCatalog.Fixture("outbox-budget-exhausted");
        Assert.Equal(0, Run("run", "--scenario", source, "--output", Path.Combine(temp.DirectoryPath, "expected")));
        var node = JsonNode.Parse(File.ReadAllText(source))!; node["expected"] = new JsonObject();
        var path = temp.FilePath("custom.json"); File.WriteAllText(path, node.ToJsonString());
        Assert.Equal(3, Run("run", "--scenario", path, "--output", Path.Combine(temp.DirectoryPath, "unresolved")));
        node["expected"]!["lostEvents"] = 99; File.WriteAllText(path, node.ToJsonString());
        Assert.Equal(1, Run("run", "--scenario", path, "--output", Path.Combine(temp.DirectoryPath, "failed")));
        Assert.Equal(0, Run("consumer-demo", "--output", Path.Combine(temp.DirectoryPath, "consumer")));
    }
    [Fact]
    public void BundledCommandsWorkFromAnotherWorkingDirectory()
    {
        // Child process avoids changing the working directory of concurrently running tests.
        using var temp = new TempDatabase(); var start = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = temp.DirectoryPath, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(CliApplication).Assembly.Location); start.ArgumentList.Add("matrix"); start.ArgumentList.Add("--output"); start.ArgumentList.Add("reports");
        using var process = System.Diagnostics.Process.Start(start)!; var stdout = process.StandardOutput.ReadToEnd(); var stderr = process.StandardError.ReadToEnd(); process.WaitForExit();
        Assert.True(process.ExitCode == 0, stdout + stderr); Assert.True(File.Exists(Path.Combine(temp.DirectoryPath, "reports", "matrix.csv")));
    }
    [Fact]
    public void MalformedOwnershipManifestIsAnOutputError()
    {
        using var temp = new TempDatabase();
        foreach (var invalid in new[] { "[]", "{}", "{\"owner\":\"OutboxFaultLab\",\"schemaVersion\":1,\"files\":42}" })
        {
            File.WriteAllText(Path.Combine(temp.DirectoryPath, OutputDirectory.Manifest), invalid);
            Assert.Equal(2, Run("demo", "--output", temp.DirectoryPath, "--overwrite"));
        }
    }
    [Fact]
    public void OverwriteCannotReplaceAnUnregisteredFile()
    {
        using var temp = new TempDatabase();
        OutputDirectory.Prepare(temp.DirectoryPath, ["matrix.csv"], false);
        var userFile = Path.Combine(temp.DirectoryPath, "summary.json"); File.WriteAllText(userFile, "user data");
        Assert.Throws<OutputPathException>(() => OutputDirectory.Prepare(temp.DirectoryPath, ["summary.json"], true));
        Assert.Equal("user data", File.ReadAllText(userFile));
    }
}
