# Empaqueta Viriato.Rpa.Client, Viriato.Rpa.Client.Selenium y Viariato.ApiContracts (de la que dependen)
# y los sube al feed privado de NuGet de GitHub Packages.
#
#   $env:GITHUB_PACKAGES_TOKEN = '<token clásico con permiso write:packages>'
#   ./backend/sdk/publish-packages.ps1
#
# -SoloEmpaquetar genera los .nupkg sin subir nada (no hace falta token).
# GitHub no deja pisar una versión ya publicada: sube <Version> en el .csproj antes de volver a publicar.
param(
    [string]$Owner = 'jorgecoke11',
    [switch]$SoloEmpaquetar
)

$ErrorActionPreference = 'Stop'

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') falló (código $LASTEXITCODE)." }
}

if (-not $SoloEmpaquetar -and -not $env:GITHUB_PACKAGES_TOKEN) {
    throw 'Define la variable de entorno GITHUB_PACKAGES_TOKEN con tu token de GitHub (permiso write:packages).'
}

$backend = Resolve-Path (Join-Path $PSScriptRoot '..')
$salida = Join-Path ([IO.Path]::GetTempPath()) "viriato-packages-$(Get-Date -Format yyyyMMddHHmmss)"

Invoke-Dotnet pack (Join-Path $backend 'src/Viariato.ApiContracts') -c Release -o $salida
Invoke-Dotnet pack (Join-Path $backend 'sdk/Viriato.Rpa.Client') -c Release -o $salida
Invoke-Dotnet pack (Join-Path $backend 'sdk/Viriato.Rpa.Client.Selenium') -c Release -o $salida

Write-Host "Paquetes generados en $salida"
Get-ChildItem $salida -Filter *.nupkg | ForEach-Object { Write-Host "  $($_.Name)" }

if ($SoloEmpaquetar) { return }

Invoke-Dotnet nuget push (Join-Path $salida '*.nupkg') `
    --source "https://nuget.pkg.github.com/$Owner/index.json" `
    --api-key $env:GITHUB_PACKAGES_TOKEN `
    --skip-duplicate

Write-Host "Publicado. Revisa https://github.com/$Owner?tab=packages"
