# Validation evidence

Native validation on 2026-10-03: locked restore passed, Release build passed with zero warnings/errors, and **86 tests passed, 0 failed, 0 skipped**. The matrix command verified **30/30 cases**: 27 crash boundaries and three baselines. All ten named scenarios are covered by the test suite.

The generated demo has DB-first lost=1, publish-first ghost=1, successful outbox recovery, and outbox duplicates=1 after acceptance/crash/recovery. Consumer-demo reports naive applied=2 and idempotent applied=1 with duplicateSkipped=1. JSON/CSV reports parse, SVGs parse as XML and were visually inspected, and two matrix/consumer runs produce byte-identical reports. The published CLI successfully locates its copied inputs and runs the demo.

After cleanup, the updated Windows verification helper reran locked restore, Release build, all 86 tests, matrix, demo and consumer-demo successfully. A fresh publish to artifacts/publish-clean also ran the demo with the relocated schema. Markdown links were checked and no obsolete file references remain.

The repository cleanup retains the scenario fixtures and independent expected CSV unchanged, relocates the scenario schema to schemas/, and uses the production SQL schemas embedded in OutboxFaultLab.Sqlite. [Development instructions](development.md) describe the current verification helpers.

## Observed toolchain

| Item | Version |
|---|---|
| .NET SDK | 10.0.401, commit e34a38d2ae |
| MSBuild | 18.9.11 |
| .NET host/runtime | 10.0.12 |
| OS/architecture | Windows 10.0.26100, x64 |
| Microsoft.Data.Sqlite | 10.0.12 |
| Microsoft.NET.Test.Sdk | 17.14.1 |
| xUnit | 2.9.3 |
| xunit.runner.visualstudio | 3.1.4 |

Versions are pinned centrally and resolved in four package lock files. No optional .NET workload is required.

## Reproduce

```text
./scripts/verify.ps1
# or: bash scripts/verify.sh
dotnet publish src/OutboxFaultLab.Cli -c Release --no-restore -o artifacts/publish
dotnet artifacts/publish/OutboxFaultLab.Cli.dll demo --output artifacts/published-demo
git diff --check
```

Small executed outputs are stored under examples/. Full generated reports and machine-specific test metadata remain ignored. The [summary](../examples/outbox-after-accept/summary.json), [trace](../examples/outbox-after-accept/trace.json), [snapshots](../examples/outbox-after-accept/snapshots.json), [timeline](../examples/outbox-after-accept/timeline.svg) and [comparison CSV](../examples/demo-results.csv) are actual application outputs.

## Unrun checks and limits

Docker is unavailable on the validation machine, so Docker builds remain unverified. Remote GitHub Actions has not run. Its workflow and the optional Dockerfile are present with immutable action/image references.

These experiments simulate application exceptions with disposal rollback and a surviving broker journal. They do not test physical power loss, OS process termination, broker data loss, external consumer side effects or multi-dispatcher concurrency. Logical ticks are deterministic model units, not elapsed-time measurements. See [architecture and failure assumptions](architecture.md).
