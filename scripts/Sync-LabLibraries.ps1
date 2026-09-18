#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repoRoot

$entries = @(& git -C $repoRoot config --file .gitmodules --get-regexp '\.path$' 2>$null)
if ($LASTEXITCODE -ne 0 -or $entries.Count -eq 0) {
    Write-Host 'No lab library submodules are recorded.'
    exit 0
}

foreach ($entry in $entries) {
    $parts = $entry -split '\s+', 2
    if ($parts.Count -ne 2) {
        throw "Could not parse .gitmodules entry: $entry"
    }

    $modulePath = $parts[1].Trim()
    $repoName = Split-Path -Leaf ($modulePath -replace '/', [IO.Path]::DirectorySeparatorChar)
    $siblingPath = Join-Path $workspaceRoot $repoName
    $checkoutPath = Join-Path $repoRoot ($modulePath -replace '/', [IO.Path]::DirectorySeparatorChar)

    if (Test-Path (Join-Path $siblingPath '.git')) {
        Write-Host "using sibling $repoName at $siblingPath"
        continue
    }

    if (Test-Path $checkoutPath) {
        Write-Host "using recorded submodule $modulePath"
        continue
    }

    Write-Host "initializing $modulePath"
    & git -C $repoRoot submodule update --init --recursive -- $modulePath
    if ($LASTEXITCODE -ne 0) {
        throw "git submodule update failed for $modulePath."
    }
}

Write-Host 'Lab library synchronization complete.'
