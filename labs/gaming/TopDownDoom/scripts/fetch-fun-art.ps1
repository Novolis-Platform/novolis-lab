# One command: CC0 square characters (hero, skeleton, monster, death FX) for TopDownDoom.
# License: CC0 — https://opengameart.org/content/hand-drawn-square-characters-animated-8-directions-top-down-free-cc0

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root 'Assets\SquareCharacters'
$zip = Join-Path $env:TEMP 'square_characters_cc0.zip'
$url = 'https://opengameart.org/sites/default/files/square_characters_animated_8_directions_top_down_free_cc0.zip'

Write-Host "Downloading CC0 square characters..."
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

$staging = Join-Path $env:TEMP "square_cc0_extract_$(Get-Random)"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $staging -Force

$inner = Get-ChildItem $staging -Directory | Select-Object -First 1
if (-not $inner) { throw "Unexpected zip layout." }

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path (Join-Path $inner.FullName '*') -Destination $dest -Recurse -Force

Write-Host "Installed to $dest"
Write-Host "Run: dotnet run --project TopDownDoom.csproj"
