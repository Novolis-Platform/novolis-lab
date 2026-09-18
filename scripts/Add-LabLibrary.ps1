#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^novolis-[a-z0-9-]+$')]
    [string]$Repo
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $repoRoot
$moduleRelativePath = "submodules/$Repo"
$modulePath = Join-Path $repoRoot ($moduleRelativePath -replace '/', [IO.Path]::DirectorySeparatorChar)

if (Test-Path $modulePath) {
    throw "The lab library path already exists: $modulePath"
}

$configuredPaths = @()
try {
    $configuredPaths = @(& git -C $repoRoot config --file .gitmodules --get-regexp '\.path$' 2>$null)
} catch {
    $configuredPaths = @()
}

if ($configuredPaths -match "(^|\s)$([regex]::Escape($moduleRelativePath))$") {
    throw "The library is already recorded in .gitmodules: $moduleRelativePath"
}

$siblingPath = Join-Path $workspaceRoot $Repo
$source = if (Test-Path (Join-Path $siblingPath '.git')) {
    "../$Repo"
} else {
    "https://github.com/Novolis-Platform/$Repo.git"
}

Write-Host "Adding $Repo at $moduleRelativePath from $source"
& git -C $repoRoot submodule add $source $moduleRelativePath
if ($LASTEXITCODE -ne 0) {
    throw "git submodule add failed for $Repo."
}

Write-Host "Added $Repo. Commit .gitmodules and the submodule pointer together."
