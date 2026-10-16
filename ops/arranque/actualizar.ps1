<#
  Updates everything on the server in one go: brings the latest code of every repository in pilas.psd1 (git pull), rebuilds and
  redeploys every stack whose code is not the one last deployed (desplegado.json), makes sure the rest are running, and clears the
  images left behind. Writes it all to arranque.log, like the startup.

  The "Actualizar Viriato" shortcut that instalar.ps1 leaves on the desktop runs it with a double click; by hand:
    powershell -ExecutionPolicy Bypass -File ops\arranque\actualizar.ps1          # what changed
    powershell -ExecutionPolicy Bypass -File ops\arranque\actualizar.ps1 -Todo    # rebuild everything, changed or not
  Guide: docs/arranque-automatico.md
#>
param(
    [string]$Pilas = (Join-Path $PSScriptRoot 'pilas.psd1'),
    [switch]$Todo,
    [switch]$Esperar,
    [int]$MinutosEsperandoDocker = 10
)

. (Join-Path $PSScriptRoot 'comun.ps1')

# Which commit each stack was last deployed from, so a build that failed (or a pull done by hand) is rebuilt the next time instead of
# looking up to date. The first run knows nothing yet, so it rebuilds everything once.
$registroDeDespliegues = Join-Path $PSScriptRoot 'desplegado.json'

function LeerDesplegado {
    $desplegado = @{}
    if (Test-Path $registroDeDespliegues) {
        (Get-Content $registroDeDespliegues -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $desplegado[$_.Name] = $_.Value }
    }
    return $desplegado
}

function Actualizar {
    Anotar '--- Actualización ---'
    if (-not (EsperarDocker $MinutosEsperandoDocker)) { return 1 }

    $resueltas = LeerPilas $Pilas
    if ($null -eq $resueltas) { return 1 }

    # 1. The latest code, once per folder (two stacks can share one). Only fast-forwards: a server copy with changes of its own is left
    #    as it is, never merged behind anyone's back.
    $fallos = 0
    foreach ($carpeta in @($resueltas | ForEach-Object { $_.Carpeta } | Select-Object -Unique)) {
        if (-not (Test-Path (Join-Path $carpeta '.git'))) {
            Anotar "[$carpeta] No es un repositorio de git: no traigo nada." 'Yellow'
            continue
        }
        Anotar "[$carpeta] git pull"
        $antes = & git -C $carpeta rev-parse HEAD
        $r = Ejecutar 'git' @('-C', $carpeta, 'pull', '--ff-only')
        $despues = & git -C $carpeta rev-parse HEAD
        if ($r.Codigo -ne 0) {
            Anotar "    No se pudo traer lo nuevo (¿cambios hechos a mano en este PC, o sin conexión?): sigue la versión que había." 'Red'
            $fallos++
        } elseif ($antes -ne $despues) {
            Anotar '    Novedades:' 'Green'
            foreach ($commit in @(& git -C $carpeta log --oneline "$antes..$despues")) { Anotar "      $commit" 'Green' }
        } else {
            Anotar '    Ya estaba al día.'
        }
    }

    # 2. Rebuild every stack whose code is not the one deployed, in the order of the list (Viriato first: the extractor joins its network).
    $desplegado = LeerDesplegado
    $proyectos = ProyectosDeDocker
    $nuevas = 0
    foreach ($p in $resueltas) {
        $base = ArgumentosCompose $p $proyectos
        $clave = $p.Archivos -join '|'
        $commit = if (Test-Path (Join-Path $p.Carpeta '.git')) { & git -C $p.Carpeta rev-parse HEAD } else { $null }
        $construida = $false
        if ($Todo -or ($commit -and $desplegado[$clave] -ne $commit)) {
            # The version the build bakes in (Viriato shows it at the foot of the menu and in /api/v1/version).
            $env:GIT_SHA = if ($commit) { $commit.Substring(0, 7) } else { $null }
            $version = Join-Path $p.Carpeta 'VERSION'
            $env:APP_VERSION = if (Test-Path $version) { (Get-Content $version -TotalCount 1).Trim() } else { $null }

            # Built apart from starting, so each failure says what it is: a build that fails has touched no container.
            Anotar "[$($p.Nombre)] Construyendo la versión nueva (unos minutos; el detalle va al registro)..."
            $b = Ejecutar 'docker' ($base + @('build') + (Servicios $p)) -Callado
            if ($b.Codigo -eq 0) {
                $construida = $true
            } else {
                Anotar "[$($p.Nombre)] No se pudo construir: se queda con la versión que tenía." 'Red'
                if ($b.Salida -match "GITHUB_PACKAGES_TOKEN|NU1301|401 \(Unauthorized\)|Parameter 'password'") {
                    Anotar '    Parece que falta el token de GitHub Packages (GITHUB_PACKAGES_TOKEN): ver docs/arranque-automatico.md.' 'Yellow'
                }
                $fallos++
            }
        }

        $r = Levantar $base (Servicios $p)
        if ($r.Codigo -ne 0) {
            Anotar "[$($p.Nombre)] NO ARRANCA (código $($r.Codigo)): mira las líneas de arriba." 'Red'
            $fallos++
        } elseif ($construida) {
            Anotar "[$($p.Nombre)] Desplegada la versión nueva." 'Green'
            $nuevas++
            if ($commit) {
                $desplegado[$clave] = $commit
                $desplegado | ConvertTo-Json | Set-Content $registroDeDespliegues -Encoding UTF8
            }
        } else {
            Anotar "[$($p.Nombre)] En marcha."
        }
    }

    # 3. Each rebuild leaves the previous image behind without a name (the extractor's weighs 3 GB). Only those go: nothing in use.
    if ($nuevas -gt 0) {
        Anotar 'Borrando las imágenes viejas que ya no usa nadie...'
        [void](Ejecutar 'docker' @('image', 'prune', '-f'))
    }

    Anotar "Hecho: $nuevas de $($resueltas.Count) desplegadas de nuevo; $fallos problemas." $(if ($fallos -gt 0) { 'Red' } else { 'Green' })
    & docker ps --format 'table {{.Names}}\t{{.Status}}' | Out-Host
    return [int]($fallos -gt 0)
}

$codigo = Actualizar
if ($Esperar) { [void](Read-Host 'Pulsa Intro para cerrar') }
exit $codigo
