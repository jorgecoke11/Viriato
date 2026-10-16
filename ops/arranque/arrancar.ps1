<#
  Brings Viriato and the robots up when Windows signs in, and writes what it did to arranque.log (next to this file).

  Docker already restarts on its own every container that was running (they all have restart: unless-stopped). This covers what that
  does not: Docker Desktop not open yet, containers stopped by hand or removed with "down", and the order (Viriato before the robots
  that join its network). What is started, and in which order, is in pilas.psd1 (a copy of pilas.example.psd1).

  The shortcut that instalar.ps1 leaves in the Windows Startup folder runs it at sign-in; it can also be run by hand:
    powershell -ExecutionPolicy Bypass -File ops\arranque\arrancar.ps1
  Guide: docs/arranque-automatico.md
#>
param(
    [string]$Pilas = (Join-Path $PSScriptRoot 'pilas.psd1'),
    [int]$MinutosEsperandoDocker = 10,
    [switch]$BloquearPantalla
)

. (Join-Path $PSScriptRoot 'comun.ps1')

Anotar '--- Arranque ---'
if (-not (EsperarDocker $MinutosEsperandoDocker)) { Anotar 'No arranco nada.' 'Red'; exit 1 }

$resueltas = LeerPilas $Pilas
if ($null -eq $resueltas) { exit 1 }
$total = @((Import-PowerShellDataFile $Pilas).Pilas).Count
$fallos = $total - $resueltas.Count

$proyectos = ProyectosDeDocker
foreach ($p in $resueltas) {
    # Never build at sign-in: a missing image is an error to fix by hand (building the robots needs a token), not ten minutes of compiling.
    $argumentos = (ArgumentosCompose $p $proyectos) + @('up', '-d', '--no-build') + (Servicios $p)
    Anotar "[$($p.Nombre)] docker $($argumentos -join ' ')"
    $r = Ejecutar 'docker' $argumentos
    if ($r.Codigo -eq 0) {
        Anotar "[$($p.Nombre)] En marcha." 'Green'
    } else {
        # One stack that fails does not stop the others.
        Anotar "[$($p.Nombre)] HA FALLADO (código $($r.Codigo)); sigo con lo demás." 'Red'
        $fallos++
    }
}

Anotar "Hecho: $($total - $fallos) de $total en marcha."

if ($BloquearPantalla) { & rundll32.exe user32.dll,LockWorkStation }
if ($fallos -gt 0) { exit 1 }
