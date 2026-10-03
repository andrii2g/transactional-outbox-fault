namespace OutboxFaultLab;

public interface ITraceSink
{
    void Append(string kind, Component? component = null, string? point = null,
        string? eventId = null, string? transactionId = null,
        IReadOnlyDictionary<string, string>? details = null);
}
