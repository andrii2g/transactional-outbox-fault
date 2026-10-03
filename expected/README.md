# Expected results, not measured results

single-crash-matrix.csv specifies 30 independent expected outcomes under the
[documented failure model](../docs/architecture.md). Scenario JSON embeds the same count values
for convenient single-case execution. Tests compare actual state and journal with these
values. Strategy execution never reads this table to decide what effects to produce.

Interpretation examples:

- DB-first producer-commit.after: order survives, no publish invocation; lost=1.
- Publish-first order-insert.after: accepted event survives, order transaction rolls back;
  ghost=1 even though INSERT had executed.
- Outbox producer-commit.before: two attempted inserts but zero committed mutations.
- Outbox dispatcher publish.before: first invocation crashes before acceptance; restart
  invokes again; attempts=2, retries=1, accepted=1, duplicates=0.
- Outbox sent-update.after: tentative marker rolls back, restart publishes again;
  committed mutations=3 even though attempted mutations=4.
- Outbox sent-commit.after: marker already durable, restart emits nothing new.

All single-crash cases have restartCount=1, including cases where no committed work
remains. This is a property of the harness restart protocol, not proof of recovery work.
