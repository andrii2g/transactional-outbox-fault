# Development and verification

Use the SDK pinned by [global.json](../global.json), currently .NET 10.0.401 with latestPatch roll-forward. The solution contains Core, Sqlite, Cli and Tests. Nullable reference types, implicit usings, deterministic builds and warnings as errors are enabled in Directory.Build.props. Exact central package versions and per-project lock files are checked in. C# and reports use UTF-8/LF.

## Native gates

Run from the repository root:

```bash
dotnet restore TransactionalOutboxFaultLab.slnx --locked-mode
dotnet build TransactionalOutboxFaultLab.slnx -c Release --no-restore
dotnet test TransactionalOutboxFaultLab.slnx -c Release --no-build
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- demo --output artifacts/demo
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- matrix --output artifacts/matrix
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- consumer-demo --output artifacts/consumer
```

Choose fresh output directories or add --overwrite for registered lab reports. Full artifacts, bin/obj, TestResults and SQLite files are ignored. Keep only small useful executed examples under examples/.

The verification helpers perform locked restore, Release build, tests, matrix, demo and consumer-demo, using a fresh temporary output directory and printing its location:

```powershell
./scripts/verify.ps1
```

```bash
bash scripts/verify.sh
```

## Test coverage

The 86 tests exercise real isolated SQLite files: order/outbox rollback and reopen, foreign keys and uniqueness, marker affected-row counts, all 27 producer/dispatcher crash boundaries plus three baselines, all ten named scenarios, persisted pending-row recovery, broker rejection/ack loss/repeated crashes, exact latency and mutation accounting, and global budget boundaries.

Consumer tests cover duplicates, distinct events, every inbox/projection/commit crash hook, replay, identity collisions and overflow rollback. CLI/report tests cover malformed/duplicate/missing JSON properties, unsafe IDs, arguments and exit codes, output ownership, unrelated-file preservation, JSON/CSV parsing, SVG XML/escaping/trace retention, and a child-process matrix run from another working directory. DeterminismTests compares every report byte across two matrix runs and two consumer runs.

When changing the model, update the schema, scenario fixtures, independent expected CSV and affected tests together. Producer/dispatcher recovery must use SQLite, not observational trace or journal state. Broker retries catch modeled transient failures; crashes escape to the harness. Consumers keep inbox and projection mutation in one separate database transaction.

## Publish

```bash
dotnet publish src/OutboxFaultLab.Cli -c Release --no-restore -o artifacts/publish
dotnet artifacts/publish/OutboxFaultLab.Cli.dll demo --output artifacts/published-demo
```

The CLI project copies scenarios, expected CSV and schemas into build/publish output. SQL schemas are embedded in OutboxFaultLab.Sqlite. No Python runtime is required.

## Optional Docker

The root Dockerfile has verify and runtime targets and pins SDK/runtime manifest digests. The verify target runs locked restore, Release build, tests and matrix, then publishes the CLI. The runtime image contains the published CLI and its content and runs as the image's unprivileged APP_UID.

```bash
docker build --target verify -t outbox-fault-lab-verify .
docker build --target runtime -t outbox-fault-lab .
mkdir -p artifacts/docker
docker run --rm --user "$(id -u):$(id -g)" \
  -v "$PWD/artifacts/docker:/artifacts" \
  outbox-fault-lab demo --output /artifacts/demo
```

The mount example is for Linux/WSL. Mount only an output directory; no host ports, broker daemon or privileged mode is needed. Docker builds were not run on the validation machine because Docker is unavailable. Digests are in Dockerfile; [validation evidence](validation.md) records the actual native checks.

## CI

.github/workflows/ci.yml uses pinned action revisions, global.json, locked restore, Release build, tests, matrix and artifact upload. Remote CI has not run during local implementation; it starts when the changes are committed and pushed. Package upgrades require updating Directory.Packages.props and restoring new lock files before locked CI restore.
