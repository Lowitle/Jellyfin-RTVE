#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the plugin and creates the zip that is dropped into Jellyfin's plugin folder.

.EXAMPLE
    pwsh ./package.ps1
    powershell -ExecutionPolicy Bypass -File .\package.ps1 -Version 1.0.0.0
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.0.0.0'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path (Join-Path $root 'Jellyfin.Plugin.RtvePlay') 'Jellyfin.Plugin.RtvePlay.csproj'

Write-Host "Compilando $project ($Configuration)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "La compilacion ha fallado (codigo $LASTEXITCODE)."
}

$bin = Join-Path (Join-Path (Join-Path (Join-Path $root 'Jellyfin.Plugin.RtvePlay') 'bin') $Configuration) 'net9.0'
$dll = Join-Path $bin 'Jellyfin.Plugin.RtvePlay.dll'
if (-not (Test-Path $dll)) {
    throw "No se ha encontrado el ensamblado compilado en $bin."
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$zip = Join-Path $dist "rtveplay_$Version.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path $dll -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Paquete creado: $zip" -ForegroundColor Green
Write-Host "SHA256: $hash"
Write-Host "Contenido:"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead($zip).Entries |
    ForEach-Object { Write-Host "  - $($_.Name) ($($_.Length) bytes)" }
