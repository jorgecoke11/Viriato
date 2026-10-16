# Shared by arrancar.ps1 and actualizar.ps1 (dot-sourced): the log, waiting for Docker, and how a stack of pilas.psd1 turns into a
# docker compose command.

$ErrorActionPreference = 'Continue'
$raizViriato = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$registro = Join-Path $PSScriptRoot 'arranque.log'

# The log is kept small: past 1 MB the old one stays as arranque.log.1.
if ((Test-Path $registro) -and (Get-Item $registro).Length -gt 1MB) { Move-Item $registro "$registro.1" -Force }

function Anotar([string]$texto, [string]$color = '', [switch]$SoloRegistro) {
    $linea = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $texto
    Add-Content -Path $registro -Value $linea -Encoding UTF8
    if ($SoloRegistro) { return }
    if ($color) { Write-Host $linea -ForegroundColor $color } else { Write-Host $linea }
}

# Runs a program, writes what it says to the log and returns its exit code and its output. -Callado keeps it off the screen (a build
# prints hundreds of lines); if it fails, its last lines are shown in red so the window still says why.
function Ejecutar([string]$programa, [string[]]$argumentos, [switch]$Callado) {
    # Windows PowerShell wraps each line a program writes to stderr in an error record; the line itself is its TargetObject (an empty
    # line would otherwise read "System.Management.Automation.RemoteException").
    $salida = @(& $programa @argumentos 2>&1 | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { "$($_.TargetObject)" } else { "$_" } })
    $codigo = $LASTEXITCODE
    $lineas = @($salida | Where-Object { $_.Trim() })
    foreach ($linea in $lineas) { Anotar "    $linea" -SoloRegistro:$Callado }
    if ($Callado -and $codigo -ne 0) {
        foreach ($linea in @($lineas | Select-Object -Last 12)) { Write-Host "    $linea" -ForegroundColor Red }
    }
    return [pscustomobject]@{ Codigo = $codigo; Salida = $salida }
}

# docker compose … up -d with the images there are. Docker does not recreate a container whose network changed name: it tries to
# start the old one and fails with "network … not found" (it happened when Viriato's network became "viriato"). Then the containers
# are made again; their data lives in volumes and folders, which this does not touch.
function Levantar([string[]]$base, [string[]]$servicios) {
    $r = Ejecutar 'docker' ($base + @('up', '-d', '--no-build') + $servicios)
    if ($r.Codigo -ne 0 -and ($r.Salida -match 'network .*not found')) {
        Anotar '    Algún contenedor apunta a una red que ya no existe: los vuelvo a crear (los datos no se tocan).' 'Yellow'
        $r = Ejecutar 'docker' ($base + @('up', '-d', '--no-build', '--force-recreate') + $servicios)
    }
    return $r
}

function DockerResponde {
    & docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

# Opens Docker Desktop if it is not open and waits for it. False if it does not answer in time.
function EsperarDocker([int]$minutos) {
    if (-not (DockerResponde)) {
        $escritorio = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (-not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue) -and (Test-Path $escritorio)) {
            Anotar 'Docker Desktop no estaba abierto: lo abro.'
            Start-Process $escritorio
        }
        Anotar 'Esperando a Docker...'
        $limite = (Get-Date).AddMinutes($minutos)
        while (-not (DockerResponde)) {
            if ((Get-Date) -gt $limite) {
                Anotar "Docker no ha respondido en $minutos minutos." 'Red'
                return $false
            }
            Start-Sleep -Seconds 5
        }
    }
    Anotar 'Docker listo.'
    return $true
}

# The stacks of pilas.psd1 that exist on this PC, each with its folder and compose files as full paths. Null if there is no list.
function LeerPilas([string]$ruta) {
    if (-not (Test-Path $ruta)) {
        Anotar "No existe $ruta. Copia pilas.example.psd1 como pilas.psd1 y ajusta las carpetas." 'Red'
        return $null
    }
    $resueltas = foreach ($pila in (Import-PowerShellDataFile $ruta).Pilas) {
        $carpeta = if ([IO.Path]::IsPathRooted($pila.Carpeta)) { $pila.Carpeta } else { Join-Path $raizViriato $pila.Carpeta }
        if (-not (Test-Path $carpeta)) {
            Anotar "[$($pila.Nombre)] No existe la carpeta $carpeta; la salto." 'Red'
            continue
        }
        $carpeta = (Resolve-Path $carpeta).Path
        $archivos = @($pila.Archivos | ForEach-Object { Join-Path $carpeta $_ })
        $faltan = @($archivos | Where-Object { -not (Test-Path $_) })
        if ($faltan.Count -gt 0) {
            Anotar "[$($pila.Nombre)] Falta $($faltan -join ', '); la salto." 'Red'
            continue
        }
        [pscustomobject]@{ Nombre = $pila.Nombre; Carpeta = $carpeta; Archivos = $archivos; Pila = $pila }
    }
    return , @($resueltas)
}

function ProyectosDeDocker {
    $json = & docker compose ls -a --format json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $json) { return @() }
    $lista = ($json -join "`n") | ConvertFrom-Json
    return @($lista | ForEach-Object { $_ })
}

# The Docker project a compose file already runs under, so a stack is never started a second time under another name (that would be
# a second, empty database). Several projects for the same file: the one running, else the first.
function ProyectoExistente([string]$compose, [object[]]$proyectos) {
    $candidatos = @($proyectos | Where-Object { ($_.ConfigFiles -split ',' | ForEach-Object { $_.Trim() }) -contains $compose })
    if ($candidatos.Count -gt 1) {
        $enMarcha = @($candidatos | Where-Object { $_.Status -match 'running' })
        if ($enMarcha.Count -gt 0) { $candidatos = $enMarcha }
        Anotar ("    Aviso: este compose lo usan varios proyectos ({0}); uso {1}." -f (($candidatos | ForEach-Object { $_.Name }) -join ', '), $candidatos[0].Name) 'Yellow'
    }
    if ($candidatos.Count -gt 0) { return $candidatos[0].Name }
    return $null
}

# "compose -p … -f … --env-file … --profile …" for a stack; the caller adds the command (up, ps…). Services go last.
function ArgumentosCompose($resuelta, [object[]]$proyectos) {
    $pila = $resuelta.Pila
    $argumentos = @('compose')
    $proyecto = if ($pila.Proyecto) { $pila.Proyecto } else { ProyectoExistente $resuelta.Archivos[0] $proyectos }
    if ($proyecto) { $argumentos += @('-p', $proyecto) }
    foreach ($archivo in $resuelta.Archivos) { $argumentos += @('-f', $archivo) }
    if ($pila.Entorno) { $argumentos += @('--env-file', (Join-Path $resuelta.Carpeta $pila.Entorno)) }
    foreach ($perfil in @($pila.Perfiles)) { if ($perfil) { $argumentos += @('--profile', $perfil) } }
    return , $argumentos
}

function Servicios($resuelta) {
    return , @(@($resuelta.Pila.Servicios) | Where-Object { $_ })
}
