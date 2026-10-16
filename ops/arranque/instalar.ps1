<#
  Sets up the server so nobody has to type commands:
    - a shortcut in your own Startup folder that runs arrancar.ps1 every time you sign in to Windows (no administrator needed);
    - an "Actualizar Viriato" shortcut on the desktop that runs actualizar.ps1 (pull + redeploy what changed) with a double click.
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1                     # install
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -BloquearPantalla   # ...and lock the screen once all is up
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -Quitar             # remove both shortcuts
  Guide: docs/arranque-automatico.md
#>
param([switch]$Quitar, [switch]$BloquearPantalla)

$alIniciar = Join-Path ([Environment]::GetFolderPath('Startup')) 'Viriato - arrancar.lnk'
$enEscritorio = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Actualizar Viriato.lnk'

if ($Quitar) {
    Remove-Item $alIniciar, $enEscritorio -ErrorAction SilentlyContinue
    Write-Host 'Quitado: Viriato y los robots ya no se arrancan al iniciar sesión (Docker sigue reiniciando lo que estuviera en marcha),'
    Write-Host 'y ya no está el acceso directo del escritorio.'
    return
}

$pilas = Join-Path $PSScriptRoot 'pilas.psd1'
if (-not (Test-Path $pilas)) {
    Copy-Item (Join-Path $PSScriptRoot 'pilas.example.psd1') $pilas
    Write-Host "Creado $pilas a partir del ejemplo: revisa sus carpetas antes de nada."
}

# $opciones are for powershell.exe itself and must go before -File; $parametros go after it, to the script.
function AccesoDirecto([string]$ruta, [string]$opciones, [string]$script, [string]$parametros, [int]$ventana, [string]$descripcion) {
    $lnk = (New-Object -ComObject WScript.Shell).CreateShortcut($ruta)
    $lnk.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $partes = @('-NoProfile -ExecutionPolicy Bypass', $opciones, "-File `"$(Join-Path $PSScriptRoot $script)`"", $parametros)
    $lnk.Arguments = ($partes | Where-Object { $_ }) -join ' '
    $lnk.WorkingDirectory = $PSScriptRoot
    $lnk.WindowStyle = $ventana
    $lnk.Description = $descripcion
    $lnk.Save()
}

# At sign-in: hidden (7 = minimised, and -WindowStyle Hidden). The update: a normal window that waits for Enter at the end.
AccesoDirecto $alIniciar '-WindowStyle Hidden' 'arrancar.ps1' $(if ($BloquearPantalla) { '-BloquearPantalla' } else { '' }) 7 `
    'Arranca Viriato y los robots (ops\arranque\arrancar.ps1)'
AccesoDirecto $enEscritorio '' 'actualizar.ps1' '-Esperar' 1 'Trae lo último de Viriato y los robots y lo despliega (ops\arranque\actualizar.ps1)'

Write-Host "Instalado el arranque al iniciar sesión: $alIniciar"
Write-Host "Instalado en el escritorio: $enEscritorio  (doble clic para actualizar)"
Write-Host "Pruébalo ahora, sin reiniciar:  powershell -ExecutionPolicy Bypass -File `"$(Join-Path $PSScriptRoot 'arrancar.ps1')`""
