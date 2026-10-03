# Architecture and failure model

The lab runs one CreateOrder command per scenario, one dispatcher, and one row per dispatch iteration. All three strategies use explicit SQL against a real SQLite file. A deterministic in-memory broker retains every acceptance, including duplicates.

## Projects and components

| Project | Responsibility |
|---|---|
| OutboxFaultLab.Core | Models, fault hooks, broker, strategies, dispatcher, runners, metrics and verification |
| OutboxFaultLab.Sqlite | Connection configuration, embedded schemas, producer and consumer transactions |
| OutboxFaultLab.Cli | Arguments, scenario validation, bundled cases, output safety and reports |
| OutboxFaultLab.Tests | Domain tests, real SQLite boundaries, recovery, CLI and reproducibility |

Core has no dependency on Sqlite or Cli. Sqlite references Core; Cli references both. Constructor injection connects these components without a DI container.

CaseExecutor creates an isolated directory and initializes the producer database. ExperimentRunner executes the producer once, handles modeled stops and restarts, drains pending outbox work, reads committed snapshots, calculates metrics, and invokes the independent verifier. RuntimeFactory creates new store handles and dispatchers for recovery. Temporary databases are removed after all connections close.

| Owner | State | Survives simulated application crash? |
|---|---|---|
| Harness | Logical clock, fault occurrences, broker cursor/journal, trace, budgets, transaction ID counters | Yes |
| Producer disk | Committed Orders and Outbox rows | Yes |
| Application runtime | Connections, active transactions, selected row, consecutive retry failures | No |
| Separate consumer disk | ProcessedEvents and Projection | Yes |

Trace and journal state are observational. Strategies and dispatchers never use them to reconstruct missing orders or pending events.

## Producer strategies

- **db-first:** insert and commit the order; then publish. A crash after the commit can leave a lost event.
- **publish-first:** publish; then insert and commit the order. Acceptance followed by a crash can leave a ghost event.
- **outbox:** insert the order and frozen event payload in one transaction; commit both. Recovery dispatches committed pending rows.

The producer command is never replayed. A precommit crash rolls back the command and is not counted as a lost event. Unsafe strategies stop on modeled broker failures and have no recovery job.

## SQLite boundaries

Each connection disables pooling and sets foreign_keys=ON, journal_mode=DELETE and synchronous=FULL. Producer and consumer schemas are embedded from the SQL files in OutboxFaultLab.Sqlite; schema setup is excluded from workload metrics.

An insert or marker update follows: before hook, parameterized SQL, MutationExecuted, after hook. The mutation is tentative until commit. A commit follows: before hook, advance commitTicks, SQLite commit, TransactionCommitted with its mutation count, after hook. Disposal without a successful commit rolls back. Commit accounting is emitted before a postcommit crash can occur.

Snapshot reads use fresh connections after crash unwinding. Outbox reads select the lowest unsent Sequence and close the connection before publishing. The sent-marker update uses a separate transaction and must affect exactly one unsent row. SentTick is the tick when the update executes; the commit trace records when that marker becomes durable.

## Fault matching

FaultInjector increments a counter keyed by component, point and EventId, records HookReached, then matches the scheduled occurrence. A match is marked fired, records CrashInjected, and immediately throws SimulatedCrashException. Counters survive restart, occurrences start at one, and each scheduled entry fires once. Hooks consume no ticks.

Producer/dispatcher hooks use order-insert.before/after, outbox-insert.before/after, producer-commit.before/after, publish.before/after-accept/after-ack, sent-update.before/after, and sent-commit.before/after. Applicability depends on strategy and component; see [scenario configuration](scenarios.md).

## Broker protocol

Publish increments the global invocation count, records PublishInvoked, reaches publish.before, advances publishTicks, and consumes the next scripted outcome:

| Outcome | Result |
|---|---|
| reject | Records BrokerRejected; throws BrokerUnavailableException; no acceptance |
| accept-ack-lost | Permanently records acceptance, reaches after-accept, records AckLost, throws BrokerAcknowledgementLostException |
| accept-ack | Permanently records acceptance, reaches after-accept, records BrokerAck, reaches after-ack, returns |

A crash at publish.before counts as an invocation but consumes no broker outcome. Acceptance survives a subsequent application crash. Rejection and lost acknowledgement never reach after-ack. The journal preserves stable EventIds and immutable payload snapshots without deduplication.

## Dispatch and recovery

DispatchOne selects a persisted pending row, publishes it, then updates and commits the sent marker after acknowledgement. Only BrokerTransientException triggers retry. SimulatedCrashException escapes to the harness; no marker is written in a finally block.

The harness records a post-stop snapshot after disposal, advances restartTicks, increments the epoch and records RuntimeRestarted. A new runtime reads the post-restart state before dispatch. Each epoch begins with pollTicks; handled rows schedule the next poll. Transient failures use max(pollTicks, min(retryCapTicks, retryBaseTicks * 2^(consecutiveFailures-1))) with saturating arithmetic. Restart resets the consecutive failure count.

Publish attempts, ticks and restarts have global budgets. A final allowed successful publish can still commit its marker. An action that would exceed maxTicks does not advance the clock; its uncommitted transaction rolls back. Exhaustion leaves durable pending work unresolved. An accepted event with an uncommitted marker can be published again, producing a duplicate.

## Consumer experiment

Consumer-demo feeds acceptance order from the outbox-after-accept case into two fresh, independent consumer files. Naive mode applies every delivery to Projection. Idempotent mode checks ProcessedEvents by EventId, verifies OrderId and a SHA-256 hash of the canonical UTF-8 payload, then inserts the inbox record and updates Projection in the same transaction. A matching duplicate is skipped; a changed payload fails as an identity collision. Count and amount arithmetic is checked before SQL writes.

Consumer hooks are inbox-insert.before/after, projection-update.before/after, and commit.before/after with component=consumer. Naive mode has projection and commit hooks only. Tests inject failures at all six idempotent boundaries. Restart replays from the beginning: precommit writes roll back together, while a committed inbox record prevents a repeated effect. Consumer metrics remain separate from producer/broker metrics.

## Scope and assumptions

Crashes use exceptions and transaction disposal. They model completed atomic commit outcomes, not physical power loss or a terminated OS process. The broker is assumed to survive application failures. Eventual delivery requires durable intent, eventual broker acceptance, and sufficient retry budget. Consumer deduplication covers effects inside its SQLite transaction; external emails, HTTP calls or payments need a separate design. Logical ticks are not milliseconds or throughput measurements.

Background: [Microsoft.Data.Sqlite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions), [SQLite atomic commit](https://www.sqlite.org/atomiccommit.html), [transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html), [idempotent consumer](https://microservices.io/patterns/communication-style/idempotent-consumer.html). Exact hooks, budgets, timing and accounting are this lab's model choices.
