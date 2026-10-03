$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $LASTEXITCODE" }
}
Invoke-Dotnet restore TransactionalOutboxFaultLab.slnx --locked-mode
Invoke-Dotnet build TransactionalOutboxFaultLab.slnx -c Release --no-restore
Invoke-Dotnet test TransactionalOutboxFaultLab.slnx -c Release --no-build
$outboxRunDir = Join-Path ([IO.Path]::GetTempPath()) ('outbox-lab-' + [Guid]::NewGuid().ToString('N'))
Invoke-Dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- matrix --output (Join-Path $outboxRunDir 'matrix')
Invoke-Dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- demo --output (Join-Path $outboxRunDir 'demo')
Invoke-Dotnet run --project src/OutboxFaultLab.Cli -c Release --no-build -- consumer-demo --output (Join-Path $outboxRunDir 'consumer')
Write-Output "Reports: $outboxRunDir"
