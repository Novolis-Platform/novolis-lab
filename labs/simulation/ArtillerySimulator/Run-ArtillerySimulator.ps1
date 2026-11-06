# Launch the last local build without compiling (avoids SDK 11 preview MSBuild task-host failures).
$exe = Join-Path $PSScriptRoot '..\..\..\artifacts\bin\ArtillerySimulator\debug\ArtillerySimulator.exe'
$exe = [System.IO.Path]::GetFullPath($exe)
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Error "Missing $exe — build once from the novolis-lab tree with ProjectReference mode, then use this script."
    exit 1
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
