using Microsoft.Data.Sqlite;
namespace OutboxFaultLab.Tests;

public class ToolchainSmokeTests
{
    [Fact]
    public void FileBackedSqliteCommitsAcrossConnections()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            using (var c = new SqliteConnection($"Data Source={path};Pooling=False"))
            { c.Open(); using var q = c.CreateCommand(); q.CommandText = "CREATE TABLE Smoke (Value INTEGER); INSERT INTO Smoke VALUES (42)"; q.ExecuteNonQuery(); }
            using var read = new SqliteConnection($"Data Source={path};Pooling=False"); read.Open();
            using var query = read.CreateCommand(); query.CommandText = "SELECT Value FROM Smoke";
            Assert.Equal(42L, query.ExecuteScalar());
        }
        finally { File.Delete(path); }
    }
}
