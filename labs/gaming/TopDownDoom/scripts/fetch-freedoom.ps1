[CmdletBinding()]
param(
    [string] $Version = '0.13.0',

    [string] $OutputDirectory,

    [string] $ArchivePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = $PSScriptRoot
$projectRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'Assets\ImportedDoom'
}

$temporaryRoot = Join-Path $env:TEMP ("novolis-freedoom-" + [Guid]::NewGuid().ToString('N'))
$archive = $ArchivePath
$downloaded = $false

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    if ([string]::IsNullOrWhiteSpace($archive)) {
        $archive = Join-Path $temporaryRoot "freedoom-$Version.zip"
        $url = "https://github.com/freedoom/freedoom/releases/download/v$Version/freedoom-$Version.zip"
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
        $downloaded = $true
    }

    $archive = (Resolve-Path $archive).Path
    $extracted = Join-Path $temporaryRoot 'content'
    Expand-Archive -Path $archive -DestinationPath $extracted -Force
    $wad = Get-ChildItem -Path $extracted -Filter 'freedoom1.wad' -File -Recurse | Select-Object -First 1
    if ($null -eq $wad) {
        throw "The Freedoom archive did not contain freedoom1.wad."
    }

    & (Join-Path $scriptRoot 'extract-doom.ps1') `
        -InputWad $wad.FullName `
        -Map E1M1 `
        -ContentProfile freedoom `
        -OutputDirectory $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Freedoom extraction failed with exit code $LASTEXITCODE."
    }

    foreach ($documentName in @('COPYING.txt', 'CREDITS.txt')) {
        $document = Get-ChildItem -Path $extracted -Filter $documentName -File -Recurse | Select-Object -First 1
        if ($null -eq $document) {
            throw "The Freedoom archive did not contain $documentName."
        }

        Copy-Item -Path $document.FullName -Destination (Join-Path $OutputDirectory $documentName) -Force
    }

    if ($downloaded) {
        $hash = (Get-FileHash -Path $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Host "Downloaded Freedoom $Version SHA-256: $hash"
    }
}
finally {
    if (Test-Path $temporaryRoot) {
        Remove-Item $temporaryRoot -Recurse -Force
    }
}
