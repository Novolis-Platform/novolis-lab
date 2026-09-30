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
$cs = Join-Path $PSScriptRoot 'Graduate-Lab.cs'
$forward = @('--name', $Name, '--to', $To)
if ($OutputPath) { $forward += @('--out', $OutputPath) }
& dotnet run --file $cs -- @forward
exit $LASTEXITCODE
