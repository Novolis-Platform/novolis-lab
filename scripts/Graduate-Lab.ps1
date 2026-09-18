#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9.-]*$')]
    [string]$Name,

    [Parameter(Mandatory)]
    [ValidateSet('tools', 'utilities', 'apps')]
    [string]$To,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$labRoots = @(
    Get-ChildItem -Path (Join-Path $repoRoot 'labs') -Directory -Recurse |
        Where-Object {
            $_.Name -eq $Name -and
            $_.FullName -notmatch '[\\/](shared)([\\/]|$)'
        }
)

if ($labRoots.Count -eq 0) {
    throw "Lab '$Name' was not found under $repoRoot\labs."
}
if ($labRoots.Count -gt 1) {
    throw "Lab name '$Name' is ambiguous: $($labRoots.FullName -join ', ')"
}

$sourceRoot = $labRoots[0].FullName
$projects = @(Get-ChildItem -Path $sourceRoot -Filter '*.csproj' -File -Recurse)
if ($projects.Count -eq 0) {
    throw "Lab '$Name' does not contain a project."
}

$sharedReferences = [System.Collections.Generic.List[string]]::new()
foreach ($project in $projects) {
    $text = Get-Content -Path $project.FullName -Raw
    foreach ($match in [regex]::Matches(
        $text,
        '<ProjectReference\b[^>]*\bInclude\s*=\s*"([^"]+)"',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $include = $match.Groups[1].Value
        if ($include -match '(?i)(?:[\\/]|^)shared(?:[\\/]|$)|Novolis\.(?:Lab|Dogfooding)\.') {
            $sharedReferences.Add("$($project.FullName): $include")
        }
    }
}

if ($sharedReferences.Count -gt 0) {
    $details = $sharedReferences -join [Environment]::NewLine
    throw "Cannot graduate '$Name' while it references lab-only shared projects:`n$details"
}

$workspaceRoot = Split-Path -Parent $repoRoot
$destinationRoot = Join-Path $workspaceRoot "novolis-$To/src/$Name"
$relativeProject = "src/$Name/$Name.csproj"
$changedPaths = @("src/$Name/**")
$displayName = [regex]::Replace($Name, '(?<=[a-z])(?=[A-Z])', ' ')

$manifest = switch ($To) {
    'tools' {
        [ordered]@{
            schemaVersion = 1
            catalogVersion = '2026.1.0'
            repository = 'novolis-tools'
            tools = @(
                [ordered]@{
                    key = $Name
                    displayName = $displayName
                    project = $relativeProject
                    packageId = "Novolis.$Name"
                    command = "novolis-$($Name.ToLowerInvariant())"
                    tests = @()
                    changedPathGlobs = $changedPaths
                }
            )
        }
    }
    'utilities' {
        [ordered]@{
            schemaVersion = 1
            catalogVersion = '2026.1.0'
            repository = 'novolis-utilities'
            utilities = @(
                [ordered]@{
                    key = $Name
                    displayName = $displayName
                    project = $relativeProject
                    solution = "src/$Name/$Name.slnx"
                    ship = @('windows-zip')
                    tests = @()
                    changedPathGlobs = $changedPaths
                }
            )
        }
    }
    'apps' {
        [ordered]@{
            schemaVersion = 1
            manifestVersion = '2026.1.0'
            channels = @{}
            apps = @(
                [ordered]@{
                    key = $Name
                    choice = $Name
                    displayName = $displayName
                    sourceRoot = "src/$Name"
                    solution = "src/$Name/$Name.slnx"
                    artifactPrefix = $Name
                    stack = 'console'
                    status = 'draft'
                    projects = [ordered]@{
                        publishWindows = $relativeProject
                        linuxCi = @($relativeProject)
                        android = $null
                        maui = $null
                        tests = @()
                        all = @($relativeProject)
                    }
                    local = @('windows', 'linux')
                    ship = @('windows-inno')
                    validation = [ordered]@{
                        changedPathGlobs = $changedPaths
                        runTests = $false
                        androidCompile = $false
                        windowsWorkload = $false
                        linuxCi = $true
                        fanOutStacks = @('console')
                    }
                }
            )
        }
    }
}

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "artifacts/graduation/$Name/$To.json"
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$manifest | ConvertTo-Json -Depth 12 | Set-Content -Path $OutputPath -Encoding utf8NoBOM

$checklistPath = [IO.Path]::ChangeExtension($OutputPath, '.md')
$checklist = @"
# Graduate $Name to $To

This is a checklist and copy plan. The script does not copy or rewrite the lab.

Source:

````text
$sourceRoot
````

Destination:

````text
$destinationRoot
````

## Checklist

- [ ] Copy the host files into the destination repository.
- [ ] Replace lab-only shared code with package or destination-repo code.
- [ ] Keep committed cross-repository references as `PackageReference`.
- [ ] Add the project to the destination solution.
- [ ] Merge the generated manifest fragment from `$OutputPath`.
- [ ] Set the destination repository's release metadata and CI coverage.
- [ ] Remove the lab entry after the destination build is published.

The source contained $($projects.Count) project(s), and no lab-only shared `ProjectReference` was found.
"@
Set-Content -Path $checklistPath -Value $checklist -Encoding utf8NoBOM

Write-Host "Wrote manifest stub: $OutputPath"
Write-Host "Wrote graduation checklist: $checklistPath"
Write-Host "Copy manually from $sourceRoot to $destinationRoot."
