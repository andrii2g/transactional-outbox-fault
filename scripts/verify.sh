#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore TransactionalOutboxFaultLab.slnx --locked-mode
dotnet build TransactionalOutboxFaultLab.slnx -c Release --no-restore
dotnet test TransactionalOutboxFaultLab.slnx -c Release --no-build
outbox_run_dir="$(mktemp -d "${TMPDIR:-/tmp}/outbox-lab-verify.XXXXXX")"
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- matrix --output "$outbox_run_dir/matrix"
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- demo --output "$outbox_run_dir/demo"
dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- consumer-demo --output "$outbox_run_dir/consumer"
printf 'Reports: %s\n' "$outbox_run_dir"
