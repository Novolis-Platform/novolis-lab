[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputWad,

    [string] $Map = 'E1M1',

    [ValidateSet('freedoom', 'original-local')]
    [string] $ContentProfile = 'freedoom',

    [string[]] $PatchWad = @(),

    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$labRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$toolProject = Join-Path $labRoot '..\novolis-tools\src\Novolis.Tools.Doom.Cli\Novolis.Tools.Doom.Cli.csproj'
$toolProject = (Resolve-Path $toolProject).Path
$inputPath = (Resolve-Path $InputWad).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets\ImportedDoom'
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$arguments = @(
    'run'
    '--project'
    $toolProject
    '--'
    'extract'
    '--input'
    $inputPath
    '--map'
    $Map
    '--output'
    $outputPath
    '--content-profile'
    $ContentProfile
)

foreach ($patch in $PatchWad) {
    $patchPath = (Resolve-Path $patch).Path
    $arguments += '--patch'
    $arguments += $patchPath
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Doom WAD extraction failed with exit code $LASTEXITCODE."
}

Write-Host "Generated Doom package: $outputPath"
