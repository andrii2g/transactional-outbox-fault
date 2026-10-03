namespace OutboxFaultLab.Tests;

public class DeterminismTests
{
    [Fact]
    public void AllMatrixAndConsumerArtifactsRepeatByteForByte()
    {
        using var temp = new TempDatabase(); var app = new CliApplication();
        foreach (var command in new[] { "matrix", "consumer-demo" })
        {
            var a = Path.Combine(temp.DirectoryPath, command + "-a"); var b = Path.Combine(temp.DirectoryPath, command + "-b");
            Assert.Equal(0, app.Run([command, "--output", a], new StringWriter(), new StringWriter()));
            Assert.Equal(0, app.Run([command, "--output", b], new StringWriter(), new StringWriter()));
            var files = Directory.GetFiles(a, "*", SearchOption.AllDirectories);
            foreach (var path in files) { var other = Path.Combine(b, Path.GetRelativePath(a, path)); Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(other)); }
        }
    }
}
