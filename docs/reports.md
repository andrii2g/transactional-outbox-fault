# Metrics and result format

Metrics counts are nonnegative Int64, IDs are compared ordinally, ticks are Int64. Reports use JSON
null for undefined latency. Undefined latency is not encoded as 0, -1, NaN or Infinity.

Let O be committed orders, E the expected event IDs associated with O via scenario input,
A the multiset of accepted broker deliveries, D the distinct IDs in A, U unsent outbox IDs.
Each scenario has one command, so expected event identity is unambiguous.

| Field | Definition |
|---|---|
| committedOrders | Count O |
| acceptedDeliveries | Count A |
| distinctAcceptedEvents | Count D |
| missingEvents | Count E minus D |
| recoverablePendingEvents | Count (E minus D) intersect U |
| lostEvents | Count (E minus D) minus U |
| ghostEvents | Distinct accepted IDs whose OrderId has no committed order |
| duplicateDeliveries | Count A minus count D |
| pendingOutboxRows | Count U, including accepted-but-unmarked rows |
| publishAttempts | PublishInvoked records, including crashes at publish.before |
| publishRetries | Sum over EventId of max(0, invocations minus 1) |
| restartCount | Actual RuntimeRestarted records |
| committedRowMutations | Sum mutation counts in TransactionCommitted |
| attemptedRowMutations | Successful INSERT/UPDATE executions, even if rolled back |
| extraWrites | committedRowMutations minus committedOrders |
| transactionCommits | Producer and marker commits; excludes setup and consumers |
| deliveryLatencyTicks | First acceptance tick minus order commit tick, if both exist |
| recoveryLatencyTicks | First acceptance minus restart tick for first restart with a missing committed event |
| recoveryDrainTicks | Last marker commit minus first restart tick with pending rows; only if those rows drain |

Delivery latency can be negative for publish-first because publication preceded the business commit. For a ghost it is null. Other latency fields are nonnegative.
If an event was accepted before restart, recoveryLatencyTicks is null even if it is
republished. recoveryDrainTicks measures closure of the stale-marker window.
An unresolved missing event has null recovery latency and unresolved status.
If no restart has a committed missing event, recovery latency is null.

Reports separate execution status from consistency and verification:

- executionStatus: completed | unresolved
- consistency: consistent | missing | ghost (derive; duplicates alone are not missing)
- verification: passed | failed
A completed unsafe strategy can be inconsistent and still pass its expected test.
An outbox case is completed only when no pending row remains (or producer rolled back).

## Snapshots
The runner records initial, each post-stop, each post-restart, and final snapshot. Each contains tick,
epoch, committed orders, outbox rows, accepted journal count, and derived consistency
counts. Post-stop includes modeled producer broker failures as well as injected crashes.
Database snapshots are read after transaction disposal. Order rows are in OrderId order,
outbox rows in Sequence order and journal entries in acceptance order.

## Report files
`summary.json`: schemaVersion=1, scenario ID, strategy, executionStatus, verification,
metrics, expected comparisons, limitations, and snapshot summaries.
`trace.json`: schemaVersion=1 and ordered trace events.
`snapshots.json`: schemaVersion=1, full immutable snapshot objects, and acceptedJournal.
`results.csv`: one row per scenario, stable fixed column order matching expected matrix
for count fields plus the latency/status fields above. Blank CSV field represents null.
`timeline.svg`: diagram of that case.
`matrix.csv` and `matrix-summary.json`: aggregate outputs for matrix mode.
Reports contain no machine paths, machine names or wall-clock metadata. The application does not generate a run-environment report.

## Independent verifier
The verifier compares actual committed OrderId and payload identities with the scenario and journal.
It checks that all accepted payloads agree with the command, outbox rows match orders,
sent markers imply acceptance, and outbox has one row per committed order.
Every specified expected metric is compared; omitted metrics are not assumed 0.
Outbox final lost/ghost counts must be zero in all supplied completed cases. Duplicates
are not invariant failures. An unknown accepted event ID is an invariant failure.

## Consumer and output ownership

consumer-demo writes consumer-summary.json with its source accepted journal and separate naive/idempotent results, plus naive-timeline.svg and idempotent-timeline.svg. Effects, skipped duplicates, consumer snapshots and trace are kept separate from producer metrics.

Output directories must be empty or contain the .outbox-lab.json ownership manifest. Conflicting files require --overwrite; it can replace only registered report paths, and unrelated files are preserved. JSON and CSV use UTF-8 and LF. Nullable CSV fields are blank; values use invariant formatting and quoting.

SVGs are generated from the actual trace using XML APIs. They group events into Producer, SQLite, Dispatcher and Broker lanes (two separate consumer lanes for consumer reports). Horizontal position measures logical ticks, sequence labels retain tied-tick order, tentative mutations are dashed/labeled, commits are solid, crashes use X markers, and restarts use + markers. Acceptance, rejection, ack loss and duplicate EventIds are explicit text. Labels are escaped and wrapped; long traces expand the viewBox vertically.
