[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [switch]$ConfirmDisposable,
    [switch]$SummaryOnly
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
$statusRoot = Join-Path $testRoot 'status'
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
        if (-not $SummaryOnly) { Write-Output "$Name=PASS" }
    }
    catch {
        $script:failed++
        Write-Output "$Name=FAIL"
        throw
    }
}

function Reset-State {
    foreach ($path in @($sourceRoot, $remoteRoot, $flagsRoot, $statusRoot)) {
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
    param(
        [string]$RemoteHost = 'synthetic-host',
        [string]$RemoteDirectory = 'C:/SyntheticBackup',
        [string]$RemoteVerifier = 'C:/SyntheticTools/verify-backup-bundle.ps1'
    )
    $mountRepo = "type=bind,source=$repoPath,target=/workspace,readonly"
    $mountTest = "type=bind,source=$testRoot,target=/test"
    $statusPath = if (Test-Path -LiteralPath (Join-Path $flagsRoot 'status-failure')) {
        '/test/missing/status.env'
    } else {
        '/test/status/sync.env'
    }
    $arguments = @(
        'run', '--rm', '--network', 'none',
        '--mount', $mountRepo,
        '--mount', $mountTest,
        '--env', 'SYNC_SOURCE_ROOT=/test/source',
        '--env', "SYNC_REMOTE_HOST=$RemoteHost",
        '--env', 'SYNC_REMOTE_USER=synthetic-user',
        '--env', "SYNC_REMOTE_DIR=$RemoteDirectory",
        '--env', "SYNC_REMOTE_VERIFIER=$RemoteVerifier",
        '--env', 'SYNC_ALLOW_TEST_ADAPTER=1',
        '--env', 'SYNC_TEST_ADAPTER=/test/mock-transport.sh',
        '--env', "SYNC_STATUS_FILE=$statusPath",
        'postgres:16-alpine',
        'bash', '/workspace/deploy/sync-backup-bundles-offhost.sh'
    )
    $output = & docker @arguments 2>&1
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($output -join "`n") }
}

function Invoke-Verifier {
    param(
        [Parameter(Mandatory = $true)][string]$Bundle,
        [string]$DestinationRoot = (Split-Path -Parent $Bundle),
        [switch]$Preflight,
        [string[]]$ExpectedFileName = @()
    )
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $verifierScript, '-DestinationRoot', $DestinationRoot, '-BundleDirectory', $Bundle)
    if ($Preflight) {
        $arguments += '-Preflight'
        $arguments += '-ExpectedFileName'
        $arguments += $ExpectedFileName
    }
    $output = & $pwsh @arguments 2>&1
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
    validate-root)
        exit 0
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
    preflight)
        names_file="$4"
        expected=''
        while IFS= read -r expected_name; do
            [ -z "$expected_name" ] || expected="$expected $expected_name"
        done < "$names_file"
        for entry in "$bundle_dir"/* "$bundle_dir"/.[!.]*; do
            [ -e "$entry" ] || continue
            base="$(basename -- "$entry")"
            case " $expected SHA256SUMS .BACKUP_OK.pending " in
                *" $base "*) ;;
                *) exit 65 ;;
            esac
        done
        ;;
    copy)
        source_file="$4"
        remote_file="$5"
        [ ! -f "$flag_dir/scp-failure" ] || exit 41
        cp -f -- "$source_file" "$bundle_dir/$remote_file"
        ;;
    verify)
        [ ! -f "$flag_dir/verifier-failure" ] || exit 44
        if [ -f "$flag_dir/corrupt-after-copy" ]; then
            payload="$(find "$bundle_dir" -maxdepth 1 -type f ! -name SHA256SUMS ! -name BACKUP_OK ! -name '.BACKUP_OK.pending' | head -n 1)"
            [ -z "$payload" ] || printf 'corruption' >> "$payload"
            rm -f -- "$flag_dir/corrupt-after-copy"
        fi
        tr -d '\r' < "$bundle_dir/SHA256SUMS" > "$bundle_dir/.manifest.normalized"
        set +e
        (cd -- "$bundle_dir" && sha256sum -c -- .manifest.normalized >/dev/null 2>&1)
        result=$?
        set -e
        rm -f -- "$bundle_dir/.manifest.normalized"
        exit "$result"
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
        Assert-True ($result.ExitCode -ne 0) "Corrupted local bundle unexpectedly succeeded. Output: $($result.Output)"
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

    Complete-Test 'REMOTE_VERIFIER_FAILURE_FAILS_WITHOUT_MARKER' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'verifier-failure'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Verifier failure unexpectedly succeeded.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $remoteRoot '2026-10-08_120000/BACKUP_OK'))) 'Marker was published after verifier failure.'
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

    Complete-Test 'PARTIAL_WITH_EXTRA_STALE_PAYLOAD_FAILS' {
        Reset-State
        New-Bundle | Out-Null
        $partial = Join-Path $remoteRoot '2026-10-08_120000'
        New-Item -ItemType Directory -Path $partial | Out-Null
        Write-Utf8NoBomLf -Path (Join-Path $partial 'stale.bin') -Content 'stale-evidence'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Partial bundle with stale payload unexpectedly succeeded.'
        Assert-True (Test-Path -LiteralPath (Join-Path $partial 'stale.bin')) 'Stale evidence was deleted.'
        Assert-True (-not (Test-Path -LiteralPath (Join-Path $partial 'BACKUP_OK'))) 'Marker was published for unsafe partial bundle.'
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

    Complete-Test 'EXISTING_MARKER_WITH_EXTRA_PAYLOAD_FAILS' {
        Reset-State
        $bundle = New-Bundle
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'unexpected.bin') -Content 'unexpected'
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Complete bundle with extra payload unexpectedly succeeded.'
    }

    Complete-Test 'COMPLETE_BUNDLE_WITH_EXTRA_DIRECTORY_FAILS' {
        Reset-State
        $bundle = New-Bundle
        $unexpected = Join-Path $bundle 'unexpected-directory'
        New-Item -ItemType Directory -Path $unexpected | Out-Null
        Write-Utf8NoBomLf -Path (Join-Path $unexpected 'evidence.txt') -Content 'preserve-evidence'
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Complete bundle with an extra directory unexpectedly succeeded.'
        Assert-True (Test-Path -LiteralPath (Join-Path $unexpected 'evidence.txt') -PathType Leaf) 'Unexpected directory evidence was modified or removed.'
    }

    Complete-Test 'COMPLETE_BUNDLE_WITH_EXTRA_JUNCTION_FAILS' {
        Reset-State
        $bundle = New-Bundle
        $target = Join-Path $testRoot 'complete-extra-junction-target'
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Write-Utf8NoBomLf -Path (Join-Path $target 'evidence.txt') -Content 'preserve-junction-evidence'
        $junction = Join-Path $bundle 'unexpected-junction'
        New-Item -ItemType Junction -Path $junction -Target $target | Out-Null
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Complete bundle with an extra junction unexpectedly succeeded.'
        Assert-True (Test-Path -LiteralPath (Join-Path $target 'evidence.txt') -PathType Leaf) 'Junction target evidence was modified or removed.'
    }

    Complete-Test 'MALICIOUS_MANIFEST_PATHS_FAIL' {
        foreach ($unsafeName in @('../../file', 'C:\outside', '/absolute/path', '\\network\share', "file';exit 0;#", '$(expression)', 'name;command', 'name|command', 'name&command', 'name with space')) {
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

    Complete-Test 'EMPTY_MANIFEST_LINE_FAILS' {
        Reset-State
        $bundle = New-Bundle
        $manifest = Join-Path $bundle 'SHA256SUMS'
        $valid = Get-Content -Raw -LiteralPath $manifest
        Write-Utf8NoBomLf -Path $manifest -Content "$valid`n`n"
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Manifest with an empty line unexpectedly succeeded.'
    }

    Complete-Test 'CRLF_GNU_MANIFEST_PASSES' {
        Reset-State
        $bundle = New-Bundle
        $manifest = Join-Path $bundle 'SHA256SUMS'
        $line = (Get-Content -LiteralPath $manifest | Select-Object -First 1)
        [IO.File]::WriteAllText($manifest, "$line`r`n", [Text.UTF8Encoding]::new($false))
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -eq 0) $result.Output
    }

    Complete-Test 'DUPLICATE_MANIFEST_ENTRY_FAILS' {
        Reset-State
        $bundle = New-Bundle
        $hash = (Get-FileHash -LiteralPath (Join-Path $bundle 'database.dump') -Algorithm SHA256).Hash
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "$hash  database.dump`n$hash *DATABASE.DUMP`n"
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Duplicate manifest entry unexpectedly succeeded.'
    }

    Complete-Test 'GNU_BINARY_MARKER_MANIFEST_PASSES' {
        Reset-State
        $bundle = New-Bundle
        $hash = (Get-FileHash -LiteralPath (Join-Path $bundle 'database.dump') -Algorithm SHA256).Hash
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'SHA256SUMS') -Content "$hash *database.dump`n"
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -eq 0) $result.Output
    }

    Complete-Test 'UNLISTED_REMOTE_PAYLOAD_FAILS' {
        Reset-State
        $bundle = New-Bundle
        Write-Utf8NoBomLf -Path (Join-Path $bundle 'unexpected.bin') -Content 'not-listed'
        $result = Invoke-Verifier -Bundle $bundle
        Assert-True ($result.ExitCode -ne 0) 'Unlisted remote payload unexpectedly succeeded.'
    }

    Complete-Test 'BUNDLE_JUNCTION_IS_REJECTED' {
        Reset-State
        $target = Join-Path $testRoot 'junction-target'
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
        New-Item -ItemType Directory -Path $target | Out-Null
        $payload = Join-Path $target 'database.dump'
        Write-Utf8NoBomLf -Path $payload -Content 'junction-payload'
        $hash = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash
        Write-Utf8NoBomLf -Path (Join-Path $target 'SHA256SUMS') -Content "$hash  database.dump`n"
        $junction = Join-Path $remoteRoot '2026-10-08_120000'
        New-Item -ItemType Junction -Path $junction -Target $target | Out-Null
        $result = Invoke-Verifier -Bundle $junction -DestinationRoot $remoteRoot
        Assert-True ($result.ExitCode -ne 0) 'Bundle junction unexpectedly succeeded.'
    }

    Complete-Test 'DESTINATION_ROOT_JUNCTION_IS_REJECTED' {
        Reset-State
        $targetRoot = Join-Path $testRoot 'root-junction-target'
        $targetBundle = Join-Path $targetRoot '2026-10-08_120000'
        New-Item -ItemType Directory -Path $targetBundle -Force | Out-Null
        $payload = Join-Path $targetBundle 'database.dump'
        Write-Utf8NoBomLf -Path $payload -Content 'root-junction-payload'
        $hash = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash
        Write-Utf8NoBomLf -Path (Join-Path $targetBundle 'SHA256SUMS') -Content "$hash  database.dump`n"
        $junctionRoot = Join-Path $testRoot 'root-junction'
        New-Item -ItemType Junction -Path $junctionRoot -Target $targetRoot | Out-Null
        $result = Invoke-Verifier -Bundle (Join-Path $junctionRoot '2026-10-08_120000') -DestinationRoot $junctionRoot
        Assert-True ($result.ExitCode -ne 0) 'Destination-root junction unexpectedly succeeded.'
    }

    Complete-Test 'PARTIAL_HARDLINK_IS_REJECTED' {
        Reset-State
        $external = Join-Path $testRoot 'hardlink-target.bin'
        Write-Utf8NoBomLf -Path $external -Content 'outside-content'
        $partial = Join-Path $remoteRoot '2026-10-08_120000'
        New-Item -ItemType Directory -Path $partial | Out-Null
        New-Item -ItemType HardLink -Path (Join-Path $partial 'database.dump') -Target $external | Out-Null
        $result = Invoke-Verifier -Bundle $partial -DestinationRoot $remoteRoot -Preflight -ExpectedFileName @('database.dump')
        Assert-True ($result.ExitCode -ne 0) 'Partial hardlink unexpectedly passed preflight.'
    }

    Complete-Test 'MALICIOUS_REMOTE_CONFIGURATION_IS_REJECTED' {
        Reset-State
        New-Bundle | Out-Null
        $badHost = Invoke-Sync -RemoteHost 'host;command'
        Assert-True ($badHost.ExitCode -ne 0) 'Remote-host command injection input was accepted.'
        $badDirectory = Invoke-Sync -RemoteDirectory 'C:/Backup;command'
        Assert-True ($badDirectory.ExitCode -ne 0) 'Remote-directory command injection input was accepted.'
        $badVerifier = Invoke-Sync -RemoteVerifier "C:/Tools/verifier'command.ps1"
        Assert-True ($badVerifier.ExitCode -ne 0) 'Verifier-path command injection input was accepted.'
        $alternateDataStream = Invoke-Sync -RemoteDirectory 'C:/Backup:stream'
        Assert-True ($alternateDataStream.ExitCode -ne 0) 'Windows alternate-data-stream path was accepted.'
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

    Complete-Test 'NO_SOURCE_BUNDLES_FAILS' {
        Reset-State
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'No-source run unexpectedly succeeded.'
        Assert-True ($result.Output -match 'event=no-source-bundles result=failure bundle=none') 'No-source failure was not reported safely.'
        $status = Get-Content -Raw -LiteralPath (Join-Path $statusRoot 'sync.env')
        Assert-True ($status -match '(?m)^result=failure$') 'No-source status did not record failure.'
        Assert-True ($status -match '(?m)^bundles_discovered=0$') 'No-source status did not record zero discovered bundles.'
    }

    Complete-Test 'STATUS_FILE_WRITE_FAILURE_FAILS_RUN' {
        Reset-State
        New-Bundle | Out-Null
        Set-Flag 'status-failure'
        $result = Invoke-Sync
        Assert-True ($result.ExitCode -ne 0) 'Status-file write failure unexpectedly returned success.'
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
