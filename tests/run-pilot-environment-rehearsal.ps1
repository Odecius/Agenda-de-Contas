[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [switch]$ConfirmDisposable,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$label = "com.abc.agendador.pilot-rehearsal=$runId"
$databaseName = 'agendador_pilot_rehearsal'
$databaseUser = 'agendador_pilot_rehearsal'
$passwordBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
$databasePassword = [Convert]::ToBase64String($passwordBytes).Replace('/', '_').Replace('+', '-')
$backupPath = Join-Path ([IO.Path]::GetTempPath()) "agendador-pilot-$runId.dump"
$containers = [Collections.Generic.List[string]]::new()
$locationPushed = $false

function Invoke-Docker {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $output = & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Disposable Docker command failed with exit code $LASTEXITCODE." }
    return $output
}

function Start-DisposableDatabase {
    param([Parameter(Mandatory = $true)][string]$Suffix)
    $name = "agendador-pilot-$Suffix-$runId"
    Invoke-Docker run --detach --rm `
        --name $name `
        --label $label `
        --tmpfs '/var/lib/postgresql/data:rw,noexec,nosuid,size=512m' `
        --publish '127.0.0.1::5432' `
        --env "POSTGRES_USER=$databaseUser" `
        --env "POSTGRES_PASSWORD=$databasePassword" `
        --env "POSTGRES_DB=$databaseName" `
        'postgres:16-alpine' | Out-Null
    $containers.Add($name)

    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $name pg_isready --username $databaseUser --dbname $databaseName 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $binding = Invoke-Docker port $name '5432/tcp'
            $port = ($binding -split ':')[-1]
            if ($port -notmatch '^\d+$') { throw 'Docker returned an invalid disposable database port.' }
            return [PSCustomObject]@{
                Name = $name
                ConnectionString = "Host=127.0.0.1;Port=$port;Database=$databaseName;Username=$databaseUser;Password=$databasePassword;SSL Mode=Disable"
            }
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'Disposable PostgreSQL did not become ready within 30 seconds.'
}

function Remove-DisposableDatabase {
    param([Parameter(Mandatory = $true)][string]$Name)
    & docker rm --force $Name 2>$null | Out-Null
    $containers.Remove($Name) | Out-Null
}

function Invoke-RehearsalPhase {
    param(
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$ConnectionString
    )
    $env:AGENDADOR_TEST_POSTGRES = $ConnectionString
    $env:AGENDADOR_PILOT_REHEARSAL_MODE = $Mode
    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Pilot rehearsal phase '$Mode' failed." }
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

    $source = Start-DisposableDatabase -Suffix 'source'
    Invoke-RehearsalPhase -Mode 'seed' -ConnectionString $source.ConnectionString

    Invoke-Docker exec $source.Name pg_dump --username $databaseUser --dbname $databaseName --format custom --file '/tmp/pilot.dump' | Out-Null
    Invoke-Docker exec $source.Name pg_restore --list '/tmp/pilot.dump' | Out-Null
    Invoke-Docker cp "$($source.Name):/tmp/pilot.dump" $backupPath | Out-Null
    $backup = Get-Item -LiteralPath $backupPath
    if ($backup.Length -le 0) { throw 'Disposable backup is empty.' }
    $checksum = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
    Write-Output 'BACKUP=PASS'
    Write-Output "BACKUP_BYTES=$($backup.Length)"
    Write-Output "BACKUP_SHA256_PREFIX=$($checksum.Substring(0, 12))"

    Remove-DisposableDatabase -Name $source.Name
    $rto = [Diagnostics.Stopwatch]::StartNew()
    $restored = Start-DisposableDatabase -Suffix 'restored'
    Invoke-Docker cp $backupPath "$($restored.Name):/tmp/pilot.dump" | Out-Null
    Invoke-Docker exec $restored.Name pg_restore --username $databaseUser --dbname $databaseName --no-owner --no-privileges '/tmp/pilot.dump' | Out-Null
    Invoke-RehearsalPhase -Mode 'validate-restored' -ConnectionString $restored.ConnectionString
    $rto.Stop()

    Write-Output 'RESTORE=PASS'
    Write-Output 'ROLLBACK_REHEARSAL=PASS'
    Write-Output "MEASURED_RTO_SECONDS=$([Math]::Ceiling($rto.Elapsed.TotalSeconds))"
    Write-Output 'PILOT_REHEARSAL=PASS'
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
        throw 'Disposable pilot rehearsal resources remain after cleanup.'
    }
}
