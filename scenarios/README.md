# Scenario inputs

matrix/ contains 30 scenarios: three baselines and 27 single-crash cases. Ten named scenarios illustrate loss, ghosts, durable recovery, rejection, lost acknowledgement, repeated crashes and budget exhaustion.

These JSON files are inputs and expected assertions, not recorded outputs. Tests execute them against real SQLite. Matrix mode matches every ID to [the independent expected CSV](../expected/single-crash-matrix.csv). Actual example reports are in [examples/](../examples/).

Wire strategies are db-first, publish-first and outbox. Broker outcomes are accept-ack, reject and accept-ack-lost. All timing values are logical ticks. Omitted scripted outcomes use defaultOutcome. Faults match exact component+point+eventId+occurrence; occurrences start at one and survive restart. Omitted expected metrics are unspecified, while null latency is explicit. Unknown and duplicate JSON properties are rejected.

See [scenario configuration](../docs/scenarios.md) and [the JSON schema](../schemas/scenario.schema.json). Run any file with `dotnet run --project src/OutboxFaultLab.Cli -- run --scenario scenarios/outbox-after-accept.json --output artifacts/one` from the repository root.
