namespace OutboxFaultLab;

public static class SchemaInitializer
{
    public static void Initialize(SqliteConnectionFactory factory, bool consumer = false)
    {
        using var stream = typeof(SchemaInitializer).Assembly.GetManifestResourceStream("OutboxFaultLab.Sqlite." + (consumer ? "ConsumerSchema.sql" : "Schema.sql"))
            ?? throw new InvalidOperationException("Schema resource missing.");
        using var reader = new StreamReader(stream); using var c = factory.Open(); using var q = c.CreateCommand();
        q.CommandText = reader.ReadToEnd(); q.ExecuteNonQuery();
    }
}
