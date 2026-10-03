namespace OutboxFaultLab;

public sealed class SimulatedCrashException(FaultContext context) : Exception("Simulated crash at " + context.Point)
{ public FaultContext Context { get; } = context; }
