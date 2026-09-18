#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9.-]*$')]
    [string]$Name,

    [Parameter(Mandatory)]
    [ValidateSet('avalonia', 'raylib', 'spectre', 'console')]
    [string]$Stack,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$labPath = Join-Path $repoRoot "labs/$Name"
$projectPath = Join-Path $labPath "$Name.csproj"
$manifestPath = Join-Path $repoRoot 'build/labs.json'
$solutionPath = Join-Path $repoRoot 'Novolis.Lab.slnx'

if ((Test-Path $labPath) -and -not $Force) {
    throw "The lab already exists: $labPath. Use -Force only when replacing it intentionally."
}

New-Item -ItemType Directory -Path $labPath -Force | Out-Null

$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Novolis.Lab.$Name</RootNamespace>
    <AssemblyName>Novolis.Lab.$Name</AssemblyName>
  </PropertyGroup>
</Project>
"@

$program = @"
Console.WriteLine("Novolis lab: $Name");
"@

$readme = @"
# $Name

Experimental **$Stack** lab host in `novolis-lab`.

Run it from the repository root:

````powershell
dotnet run --project labs/$Name/$Name.csproj
````

This host is not a release artifact. Graduate a successful experiment into
`novolis-tools`, `novolis-utilities`, or `novolis-apps` using
`scripts/Graduate-Lab.ps1`.
"@

Set-Content -Path $projectPath -Value $project -Encoding utf8NoBOM
Set-Content -Path (Join-Path $labPath 'Program.cs') -Value $program -Encoding utf8NoBOM
Set-Content -Path (Join-Path $labPath 'README.md') -Value $readme -Encoding utf8NoBOM

if (-not (Test-Path $manifestPath)) {
    throw "Labs manifest not found: $manifestPath"
}

$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$existing = @($manifest.Labs | Where-Object { $_.Key -eq $Name })
if ($existing.Count -gt 0) {
    throw "The labs manifest already contains '$Name'."
}

$manifest.Labs = @(
    @($manifest.Labs) + [pscustomobject]@{
        Key = $Name
        DisplayName = [regex]::Replace($Name, '(?<=[a-z])(?=[A-Z])', ' ')
        Projects = @("labs/$Name/$Name.csproj")
        Tests = @()
        ChangedPathGlobs = @("labs/$Name/**", "labs/shared/**")
    } | Sort-Object Key
)
Set-Content -Path $manifestPath -Value (
    $manifest | ConvertTo-Json -Depth 10
) -Encoding utf8NoBOM

& dotnet sln $solutionPath add $projectPath --solution-folder $Stack
if ($LASTEXITCODE -ne 0) {
    throw "Could not add $projectPath to $solutionPath."
}

Write-Host "Created lab $Name ($Stack): $labPath"
