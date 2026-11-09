# SilkTraceStudio entrypoint — must run with cwd in this folder so lab global.json pins SDK 10.
# Invoking `dotnet --project ...` from C:\Users\frank selects the latest installed SDK (11 preview)
# and roughly doubles Restore ("Determining projects...") cost, while also seeding an SDK-11 MSBuild node farm.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:MSBUILDDISABLENODEREUSE = '1'
$env:NOVOLIS_USE_PROJECT_REFERENCES = 'true'
dotnet run --project .\SilkTraceStudio.csproj @args
