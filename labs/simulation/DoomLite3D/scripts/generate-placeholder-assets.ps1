# Generates 64x64 placeholder PNGs for DoomLite3D when Kenney assets are not present.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$assetsRoot = Join-Path (Join-Path $PSScriptRoot "..") "Assets"
$enemiesDir = Join-Path $assetsRoot "enemies"
New-Item -ItemType Directory -Force -Path $enemiesDir | Out-Null

function Save-Sprite {
    param(
        [string]$Path,
        [System.Drawing.Color]$Body,
        [System.Drawing.Color]$Accent,
        [int]$Scale = 1
    )

    $bmp = New-Object System.Drawing.Bitmap 64, 64
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bodyW = [int](22 * $Scale)
    $bodyH = [int](30 * $Scale)
    $x = (64 - $bodyW) / 2
    $y = 64 - $bodyH - 6
    $brush = New-Object System.Drawing.SolidBrush $Body
    $g.FillEllipse($brush, $x, $y - 10, $bodyW, $bodyW)
    $g.FillRectangle($brush, $x, $y, $bodyW, $bodyH)
    $pen = New-Object System.Drawing.Pen $Accent, 2
    $g.DrawEllipse($pen, $x, $y - 10, $bodyW, $bodyW)
    $g.DrawRectangle($pen, $x, $y, $bodyW, $bodyH)

    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "Wrote $Path"
}

Save-Sprite (Join-Path $enemiesDir "imp.png") ([System.Drawing.Color]::FromArgb(255, 80, 180, 90)) ([System.Drawing.Color]::FromArgb(255, 40, 100, 50)) 1
Save-Sprite (Join-Path $enemiesDir "demon.png") ([System.Drawing.Color]::FromArgb(255, 200, 70, 60)) ([System.Drawing.Color]::FromArgb(255, 120, 30, 30)) 1
Save-Sprite (Join-Path $enemiesDir "brute.png") ([System.Drawing.Color]::FromArgb(255, 140, 100, 70)) ([System.Drawing.Color]::FromArgb(255, 80, 50, 30)) 1
Save-Sprite (Join-Path $enemiesDir "boss.png") ([System.Drawing.Color]::FromArgb(255, 160, 50, 200)) ([System.Drawing.Color]::FromArgb(255, 90, 20, 120)) 2
Save-Sprite (Join-Path $assetsRoot "weapon.png") ([System.Drawing.Color]::FromArgb(255, 110, 115, 130)) ([System.Drawing.Color]::FromArgb(255, 70, 75, 90)) 1

Write-Host "Placeholder assets ready under $assetsRoot"
