# Qué pone en marcha arrancar.ps1 al iniciar sesión, y en qué orden (Viriato primero: el extractor se une a su red para la vista).
# Copia este archivo como pilas.psd1 (no se sube a git: las carpetas son de cada PC) y ajústalo. Para que algo deje de arrancar solo,
# quítalo de aquí.
#
#   Nombre     Solo para el registro (arranque.log).
#   Carpeta    Donde están sus docker-compose: ruta completa, o relativa a la carpeta de Viriato.
#   Archivos   Sus docker-compose, en el orden en que se suman (-f).
#   Entorno    El archivo de variables (--env-file), relativo a Carpeta. Sin él, el .env de esa carpeta.
#   Perfiles   Perfiles que encender (--profile). 'tunnel' es el de Cloudflare Tunnel de Viriato.
#   Servicios  Solo estos servicios. Sin él, todos.
#   Proyecto   El nombre del proyecto de Docker (-p). Sin él, el que ya use ese compose en Docker (docker compose ls -a) y, si no hay
#              ninguno, el de la carpeta. Ponlo solo si sabes que hace falta: con un nombre nuevo Docker crea una base de datos vacía.
@{
    Pilas = @(
        @{
            Nombre   = 'Viriato'
            Carpeta  = '.'
            Archivos = @('docker-compose.prod.yml')
            Entorno  = '.env'
            Perfiles = @('tunnel')
        }
        @{
            Nombre   = 'Robots BSH (P01), con la vista del extractor en /vista/'
            Carpeta  = '..\bsh_p01_prices_extractor'
            Archivos = @('docker-compose.yml', 'docker-compose.vista-publica.yml')
        }
        @{
            Nombre   = 'IA local del trading (Ollama)'
            Carpeta  = '..\trading_robots'
            Archivos = @('docker-compose.ia.yml')
        }
        @{
            Nombre   = 'Robots de trading'
            Carpeta  = '..\trading_robots'
            Archivos = @('docker-compose.yml')
        }
    )
}
