[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BundleDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail-Verification {
    param([Parameter(Mandatory = $true)][string]$Reason)
    [Console]::Error.WriteLine("event=remote-verification result=fail reason=$Reason")
    exit 1
}

try {
    $resolvedBundle = (Resolve-Path -LiteralPath $BundleDirectory -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $resolvedBundle -PathType Container)) {
        Fail-Verification 'bundle directory is unavailable'
    }

    $manifestPath = Join-Path $resolvedBundle 'SHA256SUMS'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Fail-Verification 'manifest is missing'
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
        if (($fileInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
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
