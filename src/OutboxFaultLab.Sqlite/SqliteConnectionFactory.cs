using Microsoft.Data.Sqlite;
namespace OutboxFaultLab;

public sealed class SqliteConnectionFactory(string path)
{
    public SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try
        {
            c.Open(); using var q = c.CreateCommand();
            q.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;";
            q.ExecuteNonQuery(); return c;
        }
        catch { c.Dispose(); throw; }
    }
}
