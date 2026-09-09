[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$containerName = "agendador-pg16-gate-$runId"
$databaseName = 'agendador_gate'
$databaseUser = 'agendador_gate'
$passwordBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($passwordBytes)
$databasePassword = [Convert]::ToBase64String($passwordBytes).Replace('/', '_').Replace('+', '-')
$containerLabel = "com.abc.agendador.pg16-gate=$runId"
$locationPushed = $false
$containerStarted = $false

function Invoke-Docker {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed with exit code $LASTEXITCODE."
    }
}

try {
    Push-Location $repoPath
    $locationPushed = $true
    Invoke-Docker info --format '{{.ServerVersion}}' | Out-Null
    Invoke-Docker run --detach --rm `
        --name $containerName `
        --label $containerLabel `
        --tmpfs '/var/lib/postgresql/data:rw,noexec,nosuid,size=512m' `
        --publish '127.0.0.1::5432' `
        --env "POSTGRES_USER=$databaseUser" `
        --env "POSTGRES_PASSWORD=$databasePassword" `
        --env "POSTGRES_DB=$databaseName" `
        'postgres:16-alpine' | Out-Null
    $containerStarted = $true

    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec $containerName pg_isready --username $databaseUser --dbname $databaseName 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        throw 'PostgreSQL 16 did not become ready within 30 seconds.'
    }

    $binding = Invoke-Docker port $containerName '5432/tcp'
    $databasePort = ($binding -split ':')[-1]
    if ($databasePort -notmatch '^\d+$') {
        throw 'Docker did not return a valid temporary PostgreSQL port.'
    }
    $env:AGENDADOR_TEST_POSTGRES = "Host=127.0.0.1;Port=$databasePort;Database=$databaseName;Username=$databaseUser;Password=$databasePassword;SSL Mode=Disable"

    if (-not $SkipBuild) {
        & dotnet restore 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj'
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
        & dotnet build 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }

    & dotnet run --project 'tests/AgendadorContas.Tests/AgendadorContas.Tests.csproj' --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL gate tests failed.' }
}
finally {
    Remove-Item Env:AGENDADOR_TEST_POSTGRES -ErrorAction SilentlyContinue
    if ($containerStarted) {
        & docker rm --force $containerName 2>$null | Out-Null
    }
    if ($locationPushed) {
        Pop-Location
    }

    if ($containerStarted) {
        $residualContainers = & docker ps --all --quiet --filter "label=$containerLabel" 2>$null
        $residualVolumes = & docker volume ls --quiet --filter "label=$containerLabel" 2>$null
        $residualNetworks = & docker network ls --quiet --filter "label=$containerLabel" 2>$null
        if ($residualContainers -or $residualVolumes -or $residualNetworks) {
            throw 'Disposable PostgreSQL resources remain after cleanup.'
        }
    }
}
