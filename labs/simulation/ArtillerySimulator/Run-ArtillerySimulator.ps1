# Launch the last local build without compiling (avoids SDK 11 preview MSBuild task-host failures).
$root = $PSScriptRoot
$candidates = @(
    (Join-Path $root 'artifacts\bin\ArtillerySimulator\release\ArtillerySimulator.exe'),
    (Join-Path $root 'artifacts\bin\ArtillerySimulator\debug\ArtillerySimulator.exe'),
    (Join-Path $root '..\..\..\artifacts\bin\ArtillerySimulator\release\ArtillerySimulator.exe'),
    (Join-Path $root '..\..\..\artifacts\bin\ArtillerySimulator\debug\ArtillerySimulator.exe')
) | ForEach-Object { [System.IO.Path]::GetFullPath($_) }

$exe = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($null -eq $exe) {
    $csproj = Join-Path $root 'ArtillerySimulator.csproj'
    Write-Host "No built ArtillerySimulator.exe under artifacts\bin\ArtillerySimulator\{release|debug}. Build or run:"
    Write-Host "  dotnet build $csproj -c Release -p:NovolisUseProjectReferences=true"
    Write-Host "  dotnet run --project $csproj -p:NovolisUseProjectReferences=true -c Release"
    exit 1
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
