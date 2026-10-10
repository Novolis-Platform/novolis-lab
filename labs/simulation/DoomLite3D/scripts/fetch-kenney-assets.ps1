# Downloads Kenney Tiny Dungeon (CC0) and copies a minimal sprite subset into ../Assets/
# Falls back to generate-placeholder-assets.ps1 when download is unavailable.

$ErrorActionPreference = "Stop"
$assetsRoot = Join-Path (Join-Path $PSScriptRoot "..") "Assets"
$enemiesDir = Join-Path $assetsRoot "enemies"
$zip = Join-Path $env:TEMP "kenney_tiny-dungeon.zip"
$url = "https://kenney.nl/assets/tiny-dungeon"
$generate = Join-Path $PSScriptRoot "generate-placeholder-assets.ps1"

New-Item -ItemType Directory -Force -Path $enemiesDir | Out-Null

Write-Host "Kenney Tiny Dungeon: $url"
Write-Host "If download fails, save the zip to: $zip"
Write-Host "Then re-run this script."

if (-not (Test-Path $zip)) {
    try {
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    }
    catch {
        Write-Warning $_
        Write-Host "Running placeholder generator instead."
        & $generate
        exit 0
    }
}

$extract = Join-Path $env:TEMP "kenney_tiny-dungeon"
Expand-Archive -Path $zip -DestinationPath $extract -Force
$pngs = Get-ChildItem -Recurse $extract -Filter "*.png" | Sort-Object Length
Write-Host "Found $($pngs.Count) PNG files under $extract"

if ($pngs.Count -lt 4) {
    Write-Warning "Not enough PNGs in archive; using placeholders."
    & $generate
    exit 0
}

$targets = @(
    @{ Dest = Join-Path $enemiesDir "imp.png"; Index = 0 },
    @{ Dest = Join-Path $enemiesDir "demon.png"; Index = 1 },
    @{ Dest = Join-Path $enemiesDir "brute.png"; Index = 2 },
    @{ Dest = Join-Path $enemiesDir "boss.png"; Index = [Math]::Max(3, $pngs.Count - 1) },
    @{ Dest = Join-Path $assetsRoot "weapon.png"; Index = [Math]::Min(5, $pngs.Count - 1) }
)

foreach ($t in $targets) {
    $src = $pngs[$t.Index].FullName
    Copy-Item -Force $src $t.Dest
    Write-Host "Copied $($pngs[$t.Index].Name) -> $($t.Dest)"
}

Write-Host "Kenney assets copied to $assetsRoot"
