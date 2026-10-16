<#
  Makes arrancar.ps1 run every time you sign in to Windows: a shortcut in your own Startup folder (no administrator needed).
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1                     # install
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -BloquearPantalla   # ...and lock the screen once all is up
    powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -Quitar             # remove it
  Guide: docs/arranque-automatico.md
#>
param([switch]$Quitar, [switch]$BloquearPantalla)

$acceso = Join-Path ([Environment]::GetFolderPath('Startup')) 'Viriato - arrancar.lnk'

if ($Quitar) {
    Remove-Item $acceso -ErrorAction SilentlyContinue
    Write-Host 'Quitado: Viriato y los robots ya no se arrancan al iniciar sesión (Docker sigue reiniciando lo que estuviera en marcha).'
    return
}

$pilas = Join-Path $PSScriptRoot 'pilas.psd1'
if (-not (Test-Path $pilas)) {
    Copy-Item (Join-Path $PSScriptRoot 'pilas.example.psd1') $pilas
    Write-Host "Creado $pilas a partir del ejemplo: revisa sus carpetas antes de nada."
}

$script = Join-Path $PSScriptRoot 'arrancar.ps1'
$argumentos = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`""
if ($BloquearPantalla) { $argumentos += ' -BloquearPantalla' }

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($acceso)
$lnk.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$lnk.Arguments = $argumentos
$lnk.WorkingDirectory = $PSScriptRoot
$lnk.WindowStyle = 7
$lnk.Description = 'Arranca Viriato y los robots (ops\arranque\arrancar.ps1)'
$lnk.Save()

Write-Host "Instalado: $acceso"
Write-Host "Pruébalo ahora, sin reiniciar:  powershell -ExecutionPolicy Bypass -File `"$script`""
