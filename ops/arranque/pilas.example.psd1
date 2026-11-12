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
            # Todos los robots en un contenedor, cada uno solo mientras tiene trabajo (docs/lanzador-de-robots.md). Sustituye a los
            # contenedores sueltos de cada robot: no pongas los dos.
            Nombre   = 'Lanzador de robots, con la vista del extractor en /vista/'
            Carpeta  = 'ops\lanzador'
            Archivos = @('docker-compose.yml', 'docker-compose.red-viriato.yml')
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
