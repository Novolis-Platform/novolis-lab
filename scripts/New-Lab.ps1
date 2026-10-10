#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9.-]*$')]
    [string]$Name,

    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9-]*$')]
    [string]$Category,

    [Parameter(Mandatory)]
    [ValidateSet('avalonia', 'raylib', 'spectre', 'console')]
    [string]$Stack,

    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$cs = Join-Path $PSScriptRoot 'New-Lab.cs'
$forward = @('--name', $Name, '--category', $Category, '--stack', $Stack)
if ($Force) { $forward += '--force' }
& dotnet run --file $cs -- @forward
exit $LASTEXITCODE
