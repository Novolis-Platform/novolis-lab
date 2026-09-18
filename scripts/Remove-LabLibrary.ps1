#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^novolis-[a-z0-9-]+$')]
    [string]$Repo
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleRelativePath = "submodules/$Repo"
$configuredPaths = @(& git -C $repoRoot config --file .gitmodules --get-regexp '\.path$' 2>$null)
$matchingEntry = $configuredPaths | Where-Object {
    $_ -match "(^|\s)$([regex]::Escape($moduleRelativePath))$"
} | Select-Object -First 1

if (-not $matchingEntry) {
    throw "The library is not recorded in .gitmodules: $Repo"
}

if (-not $PSCmdlet.ShouldProcess($moduleRelativePath, 'Remove lab library submodule')) {
    return
}

& git -C $repoRoot submodule deinit --force -- $moduleRelativePath
if ($LASTEXITCODE -ne 0) {
    throw "git submodule deinit failed for $moduleRelativePath."
}

& git -C $repoRoot rm --force -- $moduleRelativePath
if ($LASTEXITCODE -ne 0) {
    throw "git rm failed for $moduleRelativePath."
}

Write-Host "Removed $Repo from the lab. The sibling checkout, if any, was left untouched."
