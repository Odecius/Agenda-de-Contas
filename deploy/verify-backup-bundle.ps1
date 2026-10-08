[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationRoot,
    [string]$BundleDirectory,
    [switch]$ValidateRoot,
    [switch]$Preflight,
    [string[]]$ExpectedFileName = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail-Verification {
    param([Parameter(Mandatory = $true)][string]$Reason)
    [Console]::Error.WriteLine("event=remote-verification result=fail reason=$Reason")
    exit 1
}

function Test-UnsafeLink {
    param([Parameter(Mandatory = $true)]$Item)
    if (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        return $true
    }
    $linkTypeProperty = $Item.PSObject.Properties['LinkType']
    return $null -ne $linkTypeProperty -and $null -ne $linkTypeProperty.Value
}

try {
    $rootItem = Get-Item -LiteralPath $DestinationRoot -Force -ErrorAction Stop
    if (-not $rootItem.PSIsContainer -or (Test-UnsafeLink $rootItem)) {
        Fail-Verification 'destination root is unavailable or unsafe'
    }
    $resolvedRoot = [IO.Path]::GetFullPath($rootItem.FullName).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

    if ($ValidateRoot) {
        Write-Output 'event=remote-root-validation result=pass'
        exit 0
    }
    if ([string]::IsNullOrWhiteSpace($BundleDirectory)) {
        Fail-Verification 'bundle directory is required'
    }

    $bundleItem = Get-Item -LiteralPath $BundleDirectory -Force -ErrorAction Stop
    if (-not $bundleItem.PSIsContainer -or (Test-UnsafeLink $bundleItem)) {
        Fail-Verification 'bundle directory is unavailable'
    }
    $resolvedBundle = [IO.Path]::GetFullPath($bundleItem.FullName).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (-not [IO.Path]::GetDirectoryName($resolvedBundle).Equals($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Fail-Verification 'bundle directory is outside the destination root'
    }

    if ($Preflight) {
        $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($fileName in $ExpectedFileName) {
            if ($fileName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or
                $fileName -in @('.', '..', 'BACKUP_OK', 'SHA256SUMS', '.BACKUP_OK.pending')) {
                Fail-Verification 'preflight contains an unsafe expected filename'
            }
            if (-not $allowed.Add($fileName)) {
                Fail-Verification 'preflight contains duplicate expected filenames'
            }
        }
        if ($allowed.Count -eq 0) {
            Fail-Verification 'preflight expected-file list is empty'
        }
        [void]$allowed.Add('SHA256SUMS')
        [void]$allowed.Add('.BACKUP_OK.pending')

        foreach ($entry in Get-ChildItem -LiteralPath $resolvedBundle -Force) {
            if ($entry.PSIsContainer -or (Test-UnsafeLink $entry) -or
                -not $allowed.Contains($entry.Name)) {
                Fail-Verification 'partial bundle contains an unsafe or unexpected entry'
            }
        }
        Write-Output "event=remote-preflight result=pass entries=$($allowed.Count)"
        exit 0
    }

    $manifestPath = Join-Path $resolvedBundle 'SHA256SUMS'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Fail-Verification 'manifest is missing'
    }
    $manifestInfo = Get-Item -LiteralPath $manifestPath -Force -ErrorAction Stop
    if (Test-UnsafeLink $manifestInfo) {
        Fail-Verification 'manifest reparse points are not allowed'
    }

    $lines = @(Get-Content -LiteralPath $manifestPath -ErrorAction Stop)
    if ($lines.Count -eq 0) {
        Fail-Verification 'manifest is empty'
    }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $lines) {
        if ($line -notmatch '^(?<hash>[0-9A-Fa-f]{64}) (?<mode>[ *])(?<name>.+)$') {
            Fail-Verification 'manifest contains an invalid line'
        }

        $expectedHash = $Matches.hash
        $fileName = $Matches.name
        if ($fileName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or
            $fileName -in @('.', '..', 'BACKUP_OK', 'SHA256SUMS') -or
            [IO.Path]::IsPathRooted($fileName) -or
            $fileName.Contains('/') -or
            $fileName.Contains('\')) {
            Fail-Verification 'manifest contains an unsafe filename'
        }
        if (-not $seen.Add($fileName)) {
            Fail-Verification 'manifest contains duplicate filenames'
        }

        $filePath = Join-Path $resolvedBundle $fileName
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            Fail-Verification 'a manifest file is missing'
        }
        $fileInfo = Get-Item -LiteralPath $filePath -Force -ErrorAction Stop
        if (Test-UnsafeLink $fileInfo) {
            Fail-Verification 'reparse points are not allowed'
        }
        $resolvedFile = (Resolve-Path -LiteralPath $filePath -ErrorAction Stop).Path
        if ([IO.Path]::GetDirectoryName($resolvedFile) -ne $resolvedBundle) {
            Fail-Verification 'a manifest file resolves outside the bundle'
        }
        $actualHash = (Get-FileHash -LiteralPath $resolvedFile -Algorithm SHA256).Hash
        if (-not $actualHash.Equals($expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
            Fail-Verification 'checksum mismatch'
        }
    }

    foreach ($file in Get-ChildItem -LiteralPath $resolvedBundle -File -Force) {
        if ($file.Name -in @('SHA256SUMS', 'BACKUP_OK')) {
            continue
        }
        if (-not $seen.Contains($file.Name)) {
            Fail-Verification 'bundle contains an unlisted file'
        }
    }

    Write-Output "event=remote-verification result=pass files=$($seen.Count)"
    exit 0
}
catch {
    Fail-Verification 'verification could not be completed'
}
