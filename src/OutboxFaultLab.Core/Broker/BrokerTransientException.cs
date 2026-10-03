namespace OutboxFaultLab;

public abstract class BrokerTransientException(string message) : Exception(message);
public sealed class BrokerUnavailableException() : BrokerTransientException("Broker rejected publication.");
public sealed class BrokerAcknowledgementLostException() : BrokerTransientException("Broker accepted; acknowledgement lost.");
