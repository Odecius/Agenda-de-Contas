[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [switch]$ConfirmDisposable,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoPath = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$label = "com.abc.agendador.offhost-rehearsal=$runId"
$databaseName = 'agendador_backup_rehearsal'
$databaseUser = 'agendador_backup_rehearsal'
$passwordBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
$databasePassword = [Convert]::ToBase64String($passwordBytes).Replace('/', '_').Replace('+', '-')
$containerName = "agendador-offhost-$runId"
$restoredContainerName = "agendador-offhost-restored-$runId"
$backupPath = Join-Path ([IO.Path]::GetTempPath()) "agendador-offhost-$runId.dump"
$containers = [Collections.Generic.List[string]]::new()
$locationPushed = $false

function Invoke-Docker {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $output = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Disposable Docker command failed with exit code $LASTEXITCODE." }
    return $output
}

function Start-Database {
    param([Parameter(Mandatory = $true)][string]$Name)
    Invoke-Docker run --detach --rm `
        --name $Name `
        --label $label `
        --tmpfs '/var/lib/postgresql/data:rw,noexec,nosuid,size=512m' `
        --publish '127.0.0.1::5432' `
        --env "POSTGRES_USER=$databaseUser" `
        --env "POSTGRES_PASSWORD=$databasePassword" `
        --env "POSTGRES_DB=$databaseName" `
        'postgres:16-alpine' | Out-Null
    $containers.Add($Name)
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $Name pg_isready --username $databaseUser --dbname $databaseName 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $port = ((Invoke-Docker port $Name '5432/tcp') -split ':')[-1]
            if ($port -notmatch '^\d+$') { throw 'Docker returned an invalid disposable database port.' }
            return "Host=127.0.0.1;Port=$port;Database=$databaseName;Username=$databaseUser;Password=$databasePassword;SSL Mode=Disable"
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'Disposable PostgreSQL did not become ready within 30 seconds.'
}

function Invoke-ExpectedFailure {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    $exitCode = & $Action
    if ($exitCode -eq 0) { throw "Failure scenario '$Name' unexpectedly succeeded." }
    Write-Output "$Name=PASS"
}

function Invoke-BackupScript {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Database = $databaseName,
        [string]$LocalDirectory = '/backup-local',
        [string]$OffHostDirectory = '/backup-offhost',
        [string]$Timestamp = '20260930T080000Z',
        [int]$RetentionDays = 30,
        [int]$MinimumKeep = 7,
        [switch]$AllowFailure
    )
    $output = & docker exec `
        --user postgres `
        --env "PGUSER=$databaseUser" `
        --env "PGPASSWORD=$databasePassword" `
        --env "BACKUP_DATABASE=$Database" `
        --env "BACKUP_LOCAL_DIR=$LocalDirectory" `
        --env "BACKUP_OFFHOST_DIR=$OffHostDirectory" `
        --env 'BACKUP_OFFHOST_ENCRYPTION_ASSERTION=external-managed-encryption' `
        --env "BACKUP_TIMESTAMP=$Timestamp" `
        --env "BACKUP_RETENTION_DAYS=$RetentionDays" `
        --env "BACKUP_MINIMUM_KEEP=$MinimumKeep" `
        $Name sh '/tmp/backup-postgresql.sh' 2>&1
    $exitCode = $LASTEXITCODE
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Disposable backup script failed with exit code $exitCode."
    }
    return $exitCode
}

try {
    if (-not $ConfirmDisposable) { throw 'Explicit -ConfirmDisposable is required.' }
    Push-Location $repoPath
    $locationPushed = $true
    Invoke-Docker info --format '{{.ServerVersion}}' | Out-Null

    if (-not $SkipBuild) {
        & dotnet restore 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj'
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
        & dotnet build 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }

    $sourceConnection = Start-Database -Name $containerName
    $env:AGENDADOR_TEST_POSTGRES = $sourceConnection
    $env:AGENDADOR_PILOT_REHEARSAL_MODE = 'seed'
    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic seed failed.' }

    Invoke-Docker cp 'deploy/backup-postgresql.sh' "$containerName`:/tmp/backup-postgresql.sh" | Out-Null
    Invoke-Docker exec --user root $containerName sh -c "mkdir -p /backup-local /backup-offhost /backup-denied; printf '%s' 'agendador-postgresql-backup-v1' > /backup-local/.agendador-backup-target; printf '%s' 'agendador-postgresql-backup-v1' > /backup-offhost/.agendador-backup-target; printf '%s' 'agendador-postgresql-backup-v1' > /backup-denied/.agendador-backup-target; chown -R postgres:postgres /backup-local /backup-offhost; chmod 500 /backup-denied; chmod 555 /tmp/backup-postgresql.sh" | Out-Null

    Invoke-BackupScript -Name $containerName | Out-Null
    Write-Output 'BACKUP=PASS'
    Write-Output 'OFFHOST_COPY=PASS'
    Write-Output 'CHECKSUM=PASS'
    Write-Output 'CATALOGUE=PASS'

    Invoke-BackupScript -Name $containerName | Out-Null
    Write-Output 'DUPLICATE_HANDLING=PASS'

    Invoke-Docker exec --user postgres $containerName sh -c 'for value in 01 02 03 04 05 06 07 08 09; do name="agendador-postgresql-20240101T0000${value}Z.dump"; cp /backup-local/agendador-postgresql-20260930T080000Z.dump "/backup-local/$name"; (cd /backup-local && sha256sum "$name" > "$name.sha256"); touch -t 202401010000 "/backup-local/$name" "/backup-local/$name.sha256"; done' | Out-Null
    $beforeRetentionCount = [int](Invoke-Docker exec $containerName sh -c "find /backup-local -maxdepth 1 -type f -name 'agendador-postgresql-*.dump' | wc -l")
    if ($beforeRetentionCount -ne 10) { throw "Retention fixture count is invalid. Expected=10 Actual=$beforeRetentionCount." }
    Invoke-BackupScript -Name $containerName -Timestamp '20260930T080050Z' -RetentionDays 30 -MinimumKeep 7 | Out-Null
    $localCount = [int](Invoke-Docker exec $containerName sh -c "find /backup-local -maxdepth 1 -type f -name 'agendador-postgresql-*.dump' | wc -l")
    $offHostCount = [int](Invoke-Docker exec $containerName sh -c "find /backup-offhost -maxdepth 1 -type f -name 'agendador-postgresql-*.dump' | wc -l")
    if ($localCount -ne 7 -or $offHostCount -ne 7) {
        throw "Retention did not preserve exactly the configured minimum restore points. Local=$localCount OffHost=$offHostCount."
    }
    Write-Output 'RETENTION=PASS'

    Invoke-ExpectedFailure -Name 'DATABASE_FAILURE' -Action { Invoke-BackupScript -Name $containerName -Database 'missing_database' -Timestamp '20260930T080100Z' -AllowFailure }
    Invoke-ExpectedFailure -Name 'DESTINATION_UNAVAILABLE' -Action { Invoke-BackupScript -Name $containerName -OffHostDirectory '/missing-offhost' -Timestamp '20260930T080200Z' -AllowFailure }
    Invoke-ExpectedFailure -Name 'PERMISSION_FAILURE' -Action { Invoke-BackupScript -Name $containerName -OffHostDirectory '/backup-denied' -Timestamp '20260930T080300Z' -AllowFailure }
    Invoke-ExpectedFailure -Name 'INSUFFICIENT_DESTINATION_PATH' -Action { Invoke-BackupScript -Name $containerName -OffHostDirectory '/' -Timestamp '20260930T080400Z' -AllowFailure }

    Invoke-Docker exec --user root $containerName sh -c "printf 'corruption' >> /backup-offhost/agendador-postgresql-20260930T080000Z.dump" | Out-Null
    Invoke-ExpectedFailure -Name 'CORRUPTED_COPY' -Action { Invoke-BackupScript -Name $containerName -AllowFailure }
    Write-Output 'CHECKSUM_MISMATCH=PASS'

    Invoke-Docker exec --user root $containerName sh -c "cp /backup-local/agendador-postgresql-20260930T080000Z.dump /tmp/verified.dump" | Out-Null
    Invoke-Docker cp "$containerName`:/tmp/verified.dump" $backupPath | Out-Null
    if ((Get-Item -LiteralPath $backupPath).Length -le 0) { throw 'Copied disposable backup is empty.' }
    Invoke-Docker exec $containerName pg_restore --list '/tmp/verified.dump' | Out-Null

    Invoke-Docker rm --force $containerName | Out-Null
    $containers.Remove($containerName) | Out-Null
    Write-Output 'DESTROY=PASS'

    $rto = [Diagnostics.Stopwatch]::StartNew()
    $restoredConnection = Start-Database -Name $restoredContainerName
    Invoke-Docker cp $backupPath "$restoredContainerName`:/tmp/verified.dump" | Out-Null
    Invoke-Docker exec $restoredContainerName pg_restore --username $databaseUser --dbname $databaseName --no-owner --no-privileges '/tmp/verified.dump' | Out-Null
    $env:AGENDADOR_TEST_POSTGRES = $restoredConnection
    $env:AGENDADOR_PILOT_REHEARSAL_MODE = 'validate-restored'
    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Restored application validation failed.' }
    $rto.Stop()

    Write-Output 'RESTORE=PASS'
    Write-Output 'APPLICATION_VALIDATION=PASS'
    Write-Output 'RPO_ARCHITECTURE=24_HOURS'
    Write-Output "MEASURED_RTO_SECONDS=$([Math]::Ceiling($rto.Elapsed.TotalSeconds))"
    Write-Output 'OFFHOST_BACKUP_REHEARSAL=PASS'
}
finally {
    Remove-Item Env:AGENDADOR_TEST_POSTGRES -ErrorAction SilentlyContinue
    Remove-Item Env:AGENDADOR_PILOT_REHEARSAL_MODE -ErrorAction SilentlyContinue
    foreach ($container in @($containers)) { & docker rm --force $container 2>$null | Out-Null }
    if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
    if ($locationPushed) { Pop-Location }

    $residualContainers = & docker ps --all --quiet --filter "label=$label" 2>$null
    $residualVolumes = & docker volume ls --quiet --filter "label=$label" 2>$null
    $residualNetworks = & docker network ls --quiet --filter "label=$label" 2>$null
    if ($residualContainers -or $residualVolumes -or $residualNetworks) {
        throw 'Disposable off-host rehearsal resources remain after cleanup.'
    }
}
