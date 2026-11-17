[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [switch]$ConfirmDisposable,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoPath = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoPath 'deploy/pilot/docker-compose.pilot-db.yml'
$envTemplate = Join-Path $repoPath 'deploy/pilot/pilot-db.env.template'
$verifier = Join-Path $repoPath 'deploy/pilot/verify-pilot-database.sh'
$runId = [Guid]::NewGuid().ToString('N')
$backupPath = Join-Path ([IO.Path]::GetTempPath()) "agenda-pilot-role-$runId.dump"
$override = New-TemporaryFile
$contexts = [Collections.Generic.List[object]]::new()
$locationPushed = $false

function New-RandomSecret {
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToHexString($bytes)
}

function New-SecretFile {
    param([Parameter(Mandatory = $true)][string]$Value)
    $file = New-TemporaryFile
    [IO.File]::WriteAllText($file.FullName, $Value, [Text.UTF8Encoding]::new($false))
    return $file.FullName
}

function Set-ComposeEnvironment {
    param([Parameter(Mandatory = $true)]$Context)
    $env:PILOT_DB_ENV_FILE = $envTemplate
    $env:PILOT_DB_ADMIN_PASSWORD_FILE = $Context.AdminSecretFile
    $env:PILOT_DB_MIGRATOR_PASSWORD_FILE = $Context.MigratorSecretFile
    $env:PILOT_DB_APP_PASSWORD_FILE = $Context.AppSecretFile
    $env:PILOT_DB_VOLUME_NAME = $Context.Volume
    $env:PILOT_DB_NETWORK_NAME = $Context.Network
}

function Invoke-Compose {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments
    )
    Set-ComposeEnvironment $Context
    $output = & docker compose -p $Context.Project -f $composeFile -f $override.FullName @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Disposable Pilot Compose command failed.' }
    return $output
}

function Start-PilotDatabase {
    param([Parameter(Mandatory = $true)][string]$Purpose)
    $suffix = "$Purpose-$($runId.Substring(0, 12))"
    $context = [PSCustomObject]@{
        Project = "agenda-pilot-role-$suffix"
        Volume = "agenda_pilot_role_$($suffix.Replace('-', '_'))"
        Network = "agenda_pilot_role_$($suffix.Replace('-', '_'))"
        AdminPassword = New-RandomSecret
        MigratorPassword = New-RandomSecret
        AppPassword = New-RandomSecret
        AdminSecretFile = $null
        MigratorSecretFile = $null
        AppSecretFile = $null
        Container = $null
        Port = $null
    }
    $context.AdminSecretFile = New-SecretFile $context.AdminPassword
    $context.MigratorSecretFile = New-SecretFile $context.MigratorPassword
    $context.AppSecretFile = New-SecretFile $context.AppPassword
    $contexts.Add($context)

    Invoke-Compose $context up --detach --wait | Out-Null
    $context.Container = (Invoke-Compose $context ps --quiet postgres).Trim()
    if (-not $context.Container) { throw 'Disposable Pilot container was not identified.' }
    $binding = & docker port $context.Container '5432/tcp'
    $context.Port = ($binding -split ':')[-1]
    if ($context.Port -notmatch '^\d+$') { throw 'Disposable Pilot loopback port was invalid.' }
    return $context
}

function Get-ConnectionString {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][ValidateSet('migrator', 'runtime')][string]$Mode
    )
    if ($Mode -eq 'migrator') {
        $user = 'agenda_pilot_migrator'
        $password = $Context.MigratorPassword
    }
    else {
        $user = 'agenda_pilot_app'
        $password = $Context.AppPassword
    }
    return "Host=127.0.0.1;Port=$($Context.Port);Database=agenda_pilot;Username=$user;Password=$password;SSL Mode=Disable"
}

function Invoke-Verifier {
    param(
        [Parameter(Mandatory = $true)]$Context,
        [Parameter(Mandatory = $true)][ValidateSet('migrator', 'runtime')][string]$Mode
    )
    if ($Mode -eq 'migrator') {
        $user = 'agenda_pilot_migrator'
        $password = $Context.MigratorPassword
    }
    else {
        $user = 'agenda_pilot_app'
        $password = $Context.AppPassword
    }
    & docker cp $verifier "$($Context.Container):/tmp/verify-pilot-database.sh" | Out-Null
    & docker exec `
        --env "PGUSER=$user" `
        --env "PGPASSWORD=$password" `
        --env 'PGDATABASE=agenda_pilot' `
        --env 'PILOT_EXPECTED_DATABASE=agenda_pilot' `
        --env "PILOT_EXPECTED_ROLE=$user" `
        --env 'PILOT_ENVIRONMENT_MARKER=agenda-pilot-v1' `
        --env "PILOT_VERIFY_MODE=$Mode" `
        $Context.Container sh /tmp/verify-pilot-database.sh
    if ($LASTEXITCODE -ne 0) { throw "Pilot $Mode verification failed." }
}

function Invoke-ExpectedDatabaseFailure {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    & $Action *> $null
    if ($LASTEXITCODE -eq 0) { throw "$Name unexpectedly succeeded." }
    Write-Output "$Name=PASS"
}

function Remove-PilotDatabase {
    param([Parameter(Mandatory = $true)]$Context)
    Set-ComposeEnvironment $Context
    & docker compose -p $Context.Project -f $composeFile -f $override.FullName down --volumes --remove-orphans 2>$null | Out-Null
    foreach ($path in @($Context.AdminSecretFile, $Context.MigratorSecretFile, $Context.AppSecretFile)) {
        if ($path -and (Test-Path -LiteralPath $path)) { Remove-Item -LiteralPath $path -Force }
    }
    $Context.AdminPassword = $null
    $Context.MigratorPassword = $null
    $Context.AppPassword = $null
}

try {
    if (-not $ConfirmDisposable) { throw 'Explicit -ConfirmDisposable is required.' }
    Push-Location $repoPath
    $locationPushed = $true
    & docker info --format '{{.ServerVersion}}' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Docker is unavailable.' }

    [IO.File]::WriteAllText(
        $override.FullName,
        "services:`n  postgres:`n    ports:`n      - '127.0.0.1::5432'`nnetworks:`n  pilot_database:`n    internal: false`n",
        [Text.UTF8Encoding]::new($false))

    if (-not $SkipBuild) {
        & dotnet restore 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj'
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
        & dotnet build 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }

    $source = Start-PilotDatabase 'source'
    $ports = & docker inspect --format '{{json .NetworkSettings.Ports}}' $source.Container
    if ($ports -notmatch '127\.0\.0\.1') { throw 'Disposable test override was not loopback-only.' }
    Write-Output 'SEPARATE_SECRETS=PASS'
    Write-Output 'DEDICATED_VOLUME=PASS'
    Write-Output 'DEDICATED_NETWORK=PASS'
    Write-Output 'COMMITTED_HOST_PORTS=NONE'

    Invoke-Verifier $source migrator
    $runtimeConnection = Get-ConnectionString $source runtime
    $migratorConnection = Get-ConnectionString $source migrator

    Invoke-ExpectedDatabaseFailure 'RUNTIME_MIGRATION_REJECTED' {
        & dotnet ef database update --no-build --connection $runtimeConnection
    }

    & dotnet ef database update --no-build --connection $migratorConnection
    if ($LASTEXITCODE -ne 0) { throw 'Migrator failed to apply Pilot migrations.' }
    Write-Output 'MIGRATOR_MIGRATIONS=PASS'
    Invoke-Verifier $source runtime

    Invoke-ExpectedDatabaseFailure 'RUNTIME_CREATE_TABLE_REJECTED' {
        & docker exec --env 'PGUSER=agenda_pilot_app' --env "PGPASSWORD=$($source.AppPassword)" --env 'PGDATABASE=agenda_pilot' $source.Container psql --command 'CREATE TABLE forbidden_runtime_ddl(id integer)'
    }
    Invoke-ExpectedDatabaseFailure 'RUNTIME_ALTER_TABLE_REJECTED' {
        & docker exec --env 'PGUSER=agenda_pilot_app' --env "PGPASSWORD=$($source.AppPassword)" --env 'PGDATABASE=agenda_pilot' $source.Container psql --command 'ALTER TABLE families ADD COLUMN forbidden_runtime_ddl integer'
    }
    Invoke-ExpectedDatabaseFailure 'RUNTIME_DROP_TABLE_REJECTED' {
        & docker exec --env 'PGUSER=agenda_pilot_app' --env "PGPASSWORD=$($source.AppPassword)" --env 'PGDATABASE=agenda_pilot' $source.Container psql --command 'DROP TABLE families'
    }

    $env:AGENDADOR_TEST_POSTGRES = $runtimeConnection
    $env:AGENDADOR_PILOT_REHEARSAL_MODE = 'seed'
    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Runtime CRUD seed failed.' }
    Write-Output 'RUNTIME_CRUD=PASS'

    & docker exec --env 'PGUSER=agenda_pilot_app' --env "PGPASSWORD=$($source.AppPassword)" --env 'PGDATABASE=agenda_pilot' $source.Container pg_dump --format custom --file /tmp/pilot.dump
    if ($LASTEXITCODE -ne 0) { throw 'Runtime backup failed.' }
    & docker exec $source.Container pg_restore --list /tmp/pilot.dump | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Runtime backup catalogue failed.' }
    & docker cp "$($source.Container):/tmp/pilot.dump" $backupPath | Out-Null
    if ((Get-Item -LiteralPath $backupPath).Length -le 0) { throw 'Runtime backup was empty.' }
    Write-Output 'RUNTIME_BACKUP=PASS'

    $restored = Start-PilotDatabase 'restored'
    Invoke-Verifier $restored migrator
    & docker cp $backupPath "$($restored.Container):/tmp/pilot.dump" | Out-Null
    & docker exec `
        --env 'PGUSER=agenda_pilot_migrator' `
        --env "PGPASSWORD=$($restored.MigratorPassword)" `
        --env 'PGDATABASE=agenda_pilot' `
        $restored.Container pg_restore --clean --if-exists --no-owner --no-privileges --dbname agenda_pilot /tmp/pilot.dump
    if ($LASTEXITCODE -ne 0) { throw 'Migrator restore failed.' }
    Invoke-Verifier $restored runtime

    $env:AGENDADOR_TEST_POSTGRES = Get-ConnectionString $restored runtime
    $env:AGENDADOR_PILOT_REHEARSAL_MODE = 'validate-restored'
    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Restored runtime application validation failed.' }
    Write-Output 'RUNTIME_RESTORE_VALIDATION=PASS'
    Write-Output 'PILOT_ROLE_HARDENING=PASS'
}
finally {
    Remove-Item Env:AGENDADOR_TEST_POSTGRES,Env:AGENDADOR_PILOT_REHEARSAL_MODE -ErrorAction SilentlyContinue
    foreach ($context in @($contexts)) { Remove-PilotDatabase $context }
    Remove-Item Env:PILOT_DB_ENV_FILE,Env:PILOT_DB_ADMIN_PASSWORD_FILE,Env:PILOT_DB_MIGRATOR_PASSWORD_FILE,Env:PILOT_DB_APP_PASSWORD_FILE,Env:PILOT_DB_VOLUME_NAME,Env:PILOT_DB_NETWORK_NAME -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $backupPath) { Remove-Item -LiteralPath $backupPath -Force }
    if (Test-Path -LiteralPath $override.FullName) { Remove-Item -LiteralPath $override.FullName -Force }
    if ($locationPushed) { Pop-Location }

    foreach ($context in @($contexts)) {
        $residualContainers = & docker ps --all --quiet --filter "label=com.docker.compose.project=$($context.Project)" 2>$null
        $residualVolume = & docker volume ls --quiet --filter "name=^$($context.Volume)$" 2>$null
        $residualNetwork = & docker network ls --quiet --filter "name=^$($context.Network)$" 2>$null
        if ($residualContainers -or $residualVolume -or $residualNetwork) {
            throw 'Disposable Pilot role-hardening resources remain after cleanup.'
        }
    }
    Write-Output 'PILOT_ROLE_HARDENING_CLEANUP=PASS'
}
