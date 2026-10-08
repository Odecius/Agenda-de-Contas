[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [switch]$ConfirmDisposable
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoPath = Split-Path -Parent $PSScriptRoot
$syncScript = Join-Path $repoPath 'deploy/sync-backup-bundles-offhost.sh'
$verifierScript = Join-Path $repoPath 'deploy/verify-backup-bundle.ps1'
$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "agendador-sync-hardening-$([Guid]::NewGuid().ToString('N'))"
$sourceRoot = Join-Path $testRoot 'source'
$remoteRoot = Join-Path $testRoot 'remote'
$flagsRoot = Join-Path $testRoot 'flags'
$adapterPath = Join-Path $testRoot 'mock-transport.sh'
$passed = 0
$failed = 0

function Write-Utf8NoBomLf {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    [IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )
    if (-not $Condition) { throw $Message }
}

function Complete-Test {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    try {
        & $Action
        $script:passed++
        Write-Output "$Name=PASS"
    }
    catch {
        $script:failed++
        Write-Output "$Name=FAIL"
        throw
    }
}

function Reset-State {
    foreach ($path in @($sourceRoot, $remoteRoot, $flagsRoot)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

function New-Bundle {
    param(
        [string]$Name = '2026-10-08_120000',
        [switch]$NoMarker,
        [switch]$NoManifest,
        [string]$Payload = 'synthetic-backup-payload'
    )
    $bundle = Join-Path $sourceRoot $Name
    New-Item -ItemType Directory -Path $bundle -Force | Out-Null
    $payloadPath = Join-Path $bundle 'database.dump'
    Write-Utf8NoBomLf -Path $payloadPath -Content $Payload
    if (-not $NoManifest) {
        $hash = (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "$hash  database.dump`n"
    }
    if (-not $NoMarker) {
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'BACKUP_OK') -Content "complete`n"
    }
    return $bundle
}

function Invoke-Sync {
    $mountRepo = "type=bind,source=$repoPath,target=/workspace,readonly"
    $mountTest = "type=bind,source=$testRoot,target=/test"
    $arguments = @(
        'run', '--rm', '--network', 'none',
        '--mount', $mountRepo,
        '--mount', $mountTest,
        '--env', 'SYNC_SOURCE_ROOT=/test/source',
        '--env', 'SYNC_REMOTE_HOST=synthetic-host',
        '--env', 'SYNC_REMOTE_USER=synthetic-user',
        '--env', 'SYNC_REMOTE_DIR=C:/SyntheticBackup',
        '--env', 'SYNC_REMOTE_VERIFIER=C:/SyntheticTools/verify-backup-bundle.ps1',
        '--env', 'SYNC_ALLOW_TEST_ADAPTER=1',
        '--env', 'SYNC_TEST_ADAPTER=/test/mock-transport.sh',
        'postgres:16-alpine',
        'bash', '/workspace/deploy/sync-backup-bundles-offhost.sh'
    )
    $output = & docker @arguments 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
}

function Invoke-Verifier {
    param([Parameter(Mandatory = $true)][string]$Bundle)
    $output = & $pwsh -NoProfile -NonInteractive -File $verifierScript -BundleDirectory $Bundle 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
}

function Set-Flag {
    param([Parameter(Mandatory = $true)][string]$Name)
    Write-Utf8NoBomLf -Path (Join-Path $flagsRoot $Name) -Content '1'
}

try {
    if (-not $ConfirmDisposable) { throw 'Explicit -ConfirmDisposable is required.' }
    & docker info --format '{{.ServerVersion}}' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Docker is unavailable.' }

    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $adapter = @'
#!/usr/bin/env bash
set -Eeuo pipefail
action="$1"
bundle_name="${2:-none}"
remote_bundle="${3:-none}"
bundle_dir="/test/remote/$bundle_name"
flag_dir='/test/flags'

case "$action" in
    connectivity)
        [ ! -f "$flag_dir/ssh-failure" ]
        ;;
    marker-exists)
        if [ -f "$bundle_dir/BACKUP_OK" ]; then exit 0; else exit 3; fi
        ;;
    directory-exists)
        if [ -d "$bundle_dir" ]; then exit 0; else exit 3; fi
        ;;
    prepare)
        mkdir -p -- "$bundle_dir"
        ;;
    clear-pending)
        rm -f -- "$bundle_dir/.BACKUP_OK.pending"
        ;;
    copy)
        source_file="$4"
        remote_file="$5"
        [ ! -f "$flag_dir/scp-failure" ] || exit 41
        cp -f -- "$source_file" "$bundle_dir/$remote_file"
        ;;
    verify)
        if [ -f "$flag_dir/corrupt-after-copy" ]; then
            payload="$(find "$bundle_dir" -maxdepth 1 -type f ! -name SHA256SUMS ! -name BACKUP_OK ! -name '.BACKUP_OK.pending' | head -n 1)"
            [ -z "$payload" ] || printf 'corruption' >> "$payload"
            rm -f -- "$flag_dir/corrupt-after-copy"
        fi
        (cd -- "$bundle_dir" && sha256sum -c -- SHA256SUMS >/dev/null 2>&1)
        ;;
    publish-marker)
        [ ! -f "$flag_dir/marker-failure" ] || exit 42
        [ ! -e "$bundle_dir/BACKUP_OK" ] || exit 43
        mv -- "$bundle_dir/.BACKUP_OK.pending" "$bundle_dir/BACKUP_OK"
        ;;
    *)
        exit 64
        ;;
esac
'@
    Write-Utf8NoBomLf -Path $adapterPath -Content $adapter

    Complete-Test 'NEW_VALID_BUNDLE' {
        Reset-State
        New-Bundle | Out-Null
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True (Test-Path -LiteralPath (Join-Path $remoteRoot '2026-10-08_120000/BACKUP_OK')) 'Remote marker was not published.'
    }

    Complete-Test 'MISSING_LOCAL_MARKER_SKIPPED' {
        Reset-State
        New-Bundle -NoMarker | Out-Null
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True ($result.Output -match 'incomplete-local-marker.*skipped') 'Missing marker was not explicitly skipped.'
    }

    Complete-Test 'MISSING_LOCAL_MANIFEST_SKIPPED' {
        Reset-State
        New-Bundle -NoManifest | Out-Null
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True ($result.Output -match 'incomplete-local-manifest.*skipped') 'Missing manifest was not explicitly skipped.'
    }

    Complete-Test 'LOCAL_CORRUPTION_FAILS' {
        Reset-State
        $bundle = New-Bundle
        Add-Content -LiteralPath (Join-Path $bundle 'database.dump') -Value 'changed'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Corrupted local bundle unexpectedly succeeded.'
    }

    Complete-Test 'SSH_FAILURE_FAILS' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'ssh-failure'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'SSH failure unexpectedly succeeded.'
    }

    Complete-Test 'SCP_FAILURE_FAILS_WITHOUT_MARKER' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'scp-failure'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'SCP failure unexpectedly succeeded.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $remoteRoot '2026-10-08_120000/BACKUP_OK'))) 'Marker was published after SCP failure.'
    }

    Complete-Test 'REMOTE_CORRUPTION_FAILS_WITHOUT_MARKER' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'corrupt-after-copy'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Remote corruption unexpectedly succeeded.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $remoteRoot '2026-10-08_120000/BACKUP_OK'))) 'Marker was published after remote corruption.'
    }

    Complete-Test 'PARTIAL_REMOTE_RECOVERS' {
        Reset-State
        New-Bundle | Out-Null
        $partial = Join-Path $remoteRoot '2026-10-08_120000'
        New-Item -ItemType Directory -Path $partial | Out-Null
        Write-Utf8NoBomLf -Path (Join-Path $partial 'database.dump') -Content 'partial'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True ($result.Output -match 'partial-recovered.*pass') 'Partial recovery was not reported.'
    }

    Complete-Test 'EXISTING_VALID_VERIFIED_AND_SKIPPED' {
        Reset-State
        New-Bundle | Out-Null
        $first = Invoke-Sync
        Assert-True ($first.ExitCode -eq 0) $first.Output
        $second = Invoke-Sync
        Assert-True ($second.ExitCode -eq 0) $second.Output
        Assert-True ($second.Output -match 'verified-existing.*pass') 'Existing valid backup was not verified.'
    }

    Complete-Test 'EXISTING_CORRUPTION_FAILS_CLOSED' {
        Reset-State
        New-Bundle | Out-Null
        $first = Invoke-Sync
        Assert-True ($first.ExitCode -eq 0) $first.Output
        $remotePayload = Join-Path $remoteRoot '2026-10-08_120000/database.dump'
        Add-Content -LiteralPath $remotePayload -Value 'corruption'
        $lengthBefore = (Get-Item -LiteralPath $remotePayload).Length
        $second = Invoke-Sync
        Assert-True ($second.ExitCode -ne 0) 'Existing corrupt backup unexpectedly succeeded.'
        Assert-True ((Get-Item -LiteralPath $remotePayload).Length -eq $lengthBefore) 'Existing corrupt evidence was overwritten.'
    }

    Complete-Test 'MALICIOUS_MANIFEST_PATHS_FAIL' {
        foreach ($unsafeName in @('../../file', 'C:\outside', '/absolute/path', '\\network\share')) {
            Reset-State
            $bundle = New-Bundle
            Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "$('a' * 64)  $unsafeName`n"
            $result = Invoke-Verifier -Bundle $bundle
            Assert-True ($result.ExitCode -ne 0) "Unsafe manifest entry was accepted: $unsafeName"
        }
    }

    Complete-Test 'MALFORMED_SHA_FAILS' {
        Reset-State
        $bundle = New-Bundle
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "invalid  database.dump`n"
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Malformed SHA unexpectedly succeeded.'
    }

    Complete-Test 'DUPLICATE_MANIFEST_ENTRY_FAILS' {
        Reset-State
        $bundle = New-Bundle
        $hash = (Get-FileHash -LiteralPath (Join-Path $bundle 'database.dump') -Algorithm SHA256).Hash
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "$hash  database.dump`n$hash *database.dump`n"
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Duplicate manifest entry unexpectedly succeeded.'
    }

    Complete-Test 'UNLISTED_REMOTE_PAYLOAD_FAILS' {
        Reset-State
        $bundle = New-Bundle
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'unexpected.bin') -Content 'not-listed'
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Unlisted remote payload unexpectedly succeeded.'
    }

    Complete-Test 'MARKER_PUBLICATION_FAILURE_FAILS' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'marker-failure'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Marker publication failure unexpectedly succeeded.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $remoteRoot '2026-10-08_120000/BACKUP_OK'))) 'Final marker exists after publication failure.'
    }

    Complete-Test 'SECOND_SUCCESSFUL_RUN_IS_IDEMPOTENT' {
        Reset-State
        New-Bundle | Out-Null
        $first = Invoke-Sync
        $second = Invoke-Sync
        Assert-True ($first.ExitCode -eq 0 -and $second.ExitCode -eq 0) 'Idempotent runs failed.'
        $markers = @(Get-ChildItem -LiteralPath $remoteRoot -Recurse -Filter 'BACKUP_OK')
        Assert-True ($markers.Count -eq 1) 'Idempotent run created unexpected markers.'
    }

    Complete-Test 'NO_SOURCE_BUNDLES_IS_SUCCESS' {
        Reset-State
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
        Assert-True ($result.Output -match 'bundles-discovered.*count=0') 'No-work behavior was not reported.'
    }

    Complete-Test 'POWERSHELL_VERIFIER_VALID_BUNDLE' {
        Reset-State
        $bundle = New-Bundle
        $manifest = Join-Path $bundle 'SHA256SUMS'
        $content = (Get-Content -Raw -LiteralPath $manifest).ToUpperInvariant()
        Write-Utf8NoBomLf -Path $manifest -Content $content
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -eq 0) $result.Output
    }

    Write-Output "SYNC_HARDENING_TESTS_PASSED=$passed"
    Write-Output "SYNC_HARDENING_TESTS_FAILED=$failed"
    if ($failed -ne 0) { exit 1 }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
