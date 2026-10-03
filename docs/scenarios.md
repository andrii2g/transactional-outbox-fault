# Scenario configuration

`run --scenario FILE` validates JSON against [scenario.schema.json](../schemas/scenario.schema.json), then checks cross-field constraints. The schema is copied beside the CLI under schemas/ for build and publish. Bundled scenarios are copied under scenarios/; demo and matrix work from any current directory. Explicit scenario paths are resolved from the current directory.

Every top-level property is required: schemaVersion=1, id, strategy, command, timing, budgets, faults, broker, expected. Unknown and duplicate JSON properties are rejected. Scenario/order/event IDs contain 1–64 ASCII letters, digits, underscores or hyphens; amounts are nonnegative Int64 cents. There is one command per scenario.

| Property | Meaning |
|---|---|
| strategy | db-first, publish-first, or outbox |
| command | orderId, eventId, amountCents; event version is 1 |
| timing | commitTicks, publishTicks, pollTicks, restartTicks, retryBaseTicks, retryCapTicks |
| budgets | maxRestarts, maxPublishAttempts, maxTicks |
| faults | component, point, eventId, occurrence for each planned crash |
| broker | outcomes array and defaultOutcome, each accept-ack, reject or accept-ack-lost |
| expected | Partial assertions on metrics and executionStatus; `{}` requests invariant-only verification |

Timing values are 1–1,000,000 logical ticks, and retryCapTicks must be at least retryBaseTicks. Budgets permit 0–100 restarts, 1–100,000 invocations and 1–1,000,000,000 ticks. Broker outcomes are consumed per invocation that reaches the broker operation; after the scripted array is exhausted, defaultOutcome repeats.

A fault matches component+point+eventId+occurrence exactly. Its EventId must equal the command EventId. Duplicate schedule entries are invalid. Occurrences are one-based and survive restart. Every scheduled fault must fire; an unreachable fault fails verification.

| Strategy/component | Reachable hook operations |
|---|---|
| db-first / producer | order-insert.before/after; producer-commit.before/after; publish.before/after-accept/after-ack |
| publish-first / producer | publish.before/after-accept/after-ack; order-insert.before/after; producer-commit.before/after |
| outbox / producer | order-insert.before/after; outbox-insert.before/after; producer-commit.before/after |
| outbox / dispatcher | publish.before/after-accept/after-ack; sent-update.before/after; sent-commit.before/after |

These are the boundaries implemented by FaultPoint. Consumer failure schedules are C# test fixtures, not producer scenario JSON or CLI options.

## Bundled cases

The [matrix directory](../scenarios/matrix/) contains 30 cases: three baselines and 27 single-crash cases, all using accept-ack outcomes and one fault-free restart. [The expected CSV](../expected/single-crash-matrix.csv) is matched one-to-one by case ID and independently compared against actual SQLite/journal/trace results. Expected values never control strategy execution.

| Named case | Demonstration |
|---|---|
| db-first-lost | Committed order with no publication after a crash |
| db-first-rejected | Rejection after business commit leaves a lost event |
| publish-first-ghost | Accepted event survives a crash before the order commits |
| publish-first-ack-lost | Acceptance without acknowledgement prevents the order transaction |
| outbox-before-dispatch | Producer postcommit crash; fresh dispatcher recovers pending intent |
| outbox-after-accept | Acceptance/crash before marker commit; recovery accepts a duplicate |
| outbox-ack-lost | Lost acknowledgement causes publication retry and duplication |
| outbox-reject-twice | Two rejections followed by success: 3 calls, 2 retries, 1 acceptance |
| outbox-crash-twice | Two postaccept crashes: 3 acceptances, 2 duplicates |
| outbox-budget-exhausted | Repeated rejection consumes the budget; missing work stays pending and unresolved |

Expected metrics may be partial: omission means unspecified, while null latency is an explicit expectation. executionStatus is completed or unresolved. A fixture explicitly expecting unresolved returns CLI exit 0 when its assertions and invariants pass. See [report definitions](reports.md) and [failure semantics](architecture.md).

Start with [outbox-after-accept.json](../scenarios/outbox-after-accept.json) when authoring a scenario; choose a new safe ID and update its faults, broker plan, timings, budgets and expected assertions together.
