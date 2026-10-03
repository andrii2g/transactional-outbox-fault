namespace OutboxFaultLab.Tests;

public sealed class TempDatabase : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "outbox-test-" + Guid.NewGuid().ToString("N"));
    public TempDatabase() => Directory.CreateDirectory(DirectoryPath);
    public string FilePath(string name = "producer.db") => Path.Combine(DirectoryPath, name);
    public void Dispose() => Directory.Delete(DirectoryPath, true);
    public SqliteConnectionFactory Initialize(bool consumer = false, string name = "producer.db")
    { var factory = new SqliteConnectionFactory(FilePath(name)); SchemaInitializer.Initialize(factory, consumer); return factory; }
}
