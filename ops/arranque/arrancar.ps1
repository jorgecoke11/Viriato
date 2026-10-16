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

$ErrorActionPreference = 'Continue'
$raizViriato = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$registro = Join-Path $PSScriptRoot 'arranque.log'

# The log is kept small: past 1 MB the old one stays as arranque.log.1.
if ((Test-Path $registro) -and (Get-Item $registro).Length -gt 1MB) { Move-Item $registro "$registro.1" -Force }

function Anotar([string]$texto) {
    $linea = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $texto
    Add-Content -Path $registro -Value $linea -Encoding UTF8
    Write-Host $linea
}

function DockerResponde {
    & docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

# The Docker project a compose file already runs under, so a stack is never started a second time under another name (that would be
# a second, empty database). Several projects for the same file: the one running, else the first.
function ProyectoExistente([string]$compose, [object[]]$proyectos) {
    $candidatos = @($proyectos | Where-Object { ($_.ConfigFiles -split ',' | ForEach-Object { $_.Trim() }) -contains $compose })
    if ($candidatos.Count -gt 1) {
        $enMarcha = @($candidatos | Where-Object { $_.Status -match 'running' })
        if ($enMarcha.Count -gt 0) { $candidatos = $enMarcha }
        Anotar ("    Aviso: este compose lo usan varios proyectos ({0}); uso {1}." -f (($candidatos | ForEach-Object { $_.Name }) -join ', '), $candidatos[0].Name)
    }
    if ($candidatos.Count -gt 0) { return $candidatos[0].Name }
    return $null
}

Anotar '--- Arranque ---'

if (-not (DockerResponde)) {
    $escritorio = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue) -and (Test-Path $escritorio)) {
        Anotar 'Docker Desktop no estaba abierto: lo abro.'
        Start-Process $escritorio
    }
    Anotar 'Esperando a Docker...'
    $limite = (Get-Date).AddMinutes($MinutosEsperandoDocker)
    while (-not (DockerResponde)) {
        if ((Get-Date) -gt $limite) {
            Anotar "Docker no ha respondido en $MinutosEsperandoDocker minutos: no arranco nada."
            exit 1
        }
        Start-Sleep -Seconds 5
    }
}
Anotar 'Docker listo.'

if (-not (Test-Path $Pilas)) {
    Anotar "No existe $Pilas. Copia pilas.example.psd1 como pilas.psd1 y ajusta las carpetas."
    exit 1
}
$config = Import-PowerShellDataFile $Pilas

$proyectos = @()
$json = & docker compose ls -a --format json 2>$null
if ($LASTEXITCODE -eq 0 -and $json) {
    $lista = ($json -join "`n") | ConvertFrom-Json
    $proyectos = @($lista | ForEach-Object { $_ })
}

$total = 0
$fallos = 0
foreach ($pila in $config.Pilas) {
    $total++
    $nombre = $pila.Nombre
    $carpeta = if ([IO.Path]::IsPathRooted($pila.Carpeta)) { $pila.Carpeta } else { Join-Path $raizViriato $pila.Carpeta }
    if (-not (Test-Path $carpeta)) {
        Anotar "[$nombre] No existe la carpeta $carpeta; la salto."
        $fallos++
        continue
    }
    $carpeta = (Resolve-Path $carpeta).Path

    $archivos = @($pila.Archivos | ForEach-Object { Join-Path $carpeta $_ })
    $faltan = @($archivos | Where-Object { -not (Test-Path $_) })
    if ($faltan.Count -gt 0) {
        Anotar "[$nombre] Falta $($faltan -join ', '); la salto."
        $fallos++
        continue
    }

    $argumentos = @('compose')
    $proyecto = if ($pila.Proyecto) { $pila.Proyecto } else { ProyectoExistente $archivos[0] $proyectos }
    if ($proyecto) { $argumentos += @('-p', $proyecto) }
    foreach ($archivo in $archivos) { $argumentos += @('-f', $archivo) }
    if ($pila.Entorno) { $argumentos += @('--env-file', (Join-Path $carpeta $pila.Entorno)) }
    foreach ($perfil in @($pila.Perfiles)) { if ($perfil) { $argumentos += @('--profile', $perfil) } }
    # Never build at sign-in: a missing image is an error to fix by hand (building the robots needs a token), not ten minutes of compiling.
    $argumentos += @('up', '-d', '--no-build')
    foreach ($servicio in @($pila.Servicios)) { if ($servicio) { $argumentos += $servicio } }

    Anotar "[$nombre] docker $($argumentos -join ' ')"
    $salida = & docker @argumentos 2>&1 | ForEach-Object { "$_" }
    $codigo = $LASTEXITCODE
    foreach ($linea in $salida) { if ($linea.Trim()) { Anotar "    $linea" } }
    if ($codigo -eq 0) {
        Anotar "[$nombre] En marcha."
    } else {
        # One stack that fails does not stop the others.
        Anotar "[$nombre] HA FALLADO (código $codigo); sigo con lo demás."
        $fallos++
    }
}

Anotar "Hecho: $($total - $fallos) de $total en marcha."

if ($BloquearPantalla) { & rundll32.exe user32.dll,LockWorkStation }
if ($fallos -gt 0) { exit 1 }
