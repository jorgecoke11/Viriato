# Desplegar Viriato en tu propia máquina y abrirlo a Internet

Todo corre con Docker en **un PC tuyo que esté siempre encendido**; para entrar desde fuera se usa **Cloudflare Tunnel** (gratis): el PC
abre una conexión *hacia* Cloudflare, así que no abres puertos del router ni te importa que la IP de casa cambie. Los robots, estén donde
estén, usan la misma dirección pública.

```
  Navegadores y robots ──HTTPS──▶ Cloudflare ◀──conexión saliente── cloudflared ─▶ web (Caddy :80) ─▶ api ─▶ postgres
                                (pone el certificado)                  └ docker compose en tu PC        └─▶ carpeta de archivos
                                                                                                         backup ─▶ SFTP (Hostinger)
```

## Lo que necesitas

1. **Un PC con Windows 10/11 siempre encendido** (o cualquier máquina con Docker). 8 GB de RAM libres bastan para empezar.
2. **Docker Desktop** (con WSL2), con *«Start Docker Desktop when you sign in»* activado, y el PC **sin suspensión**
   (Configuración → Sistema → Inicio/apagado y batería → «Nunca»). Todos los servicios llevan `restart: unless-stopped`: si el PC se
   reinicia, vuelven solos al iniciar sesión Docker Desktop.
3. **Un dominio que gestione Cloudflare.** Un túnel con dirección fija necesita que el DNS del dominio esté en Cloudflare. Opciones:
   - Mover el DNS de un dominio que ya tengas (lee **antes** todos sus registros —web, correo— para no romper nada) o,
   - comprar un dominio barato (~10 €/año) directamente en Cloudflare, o
   - para **probar** (no para robots): `cloudflared tunnel --url http://localhost:80` te da una dirección `trycloudflare.com` aleatoria que cambia cada vez.
4. Una cuenta gratuita de Cloudflare.

## 1 · Preparar la configuración

```powershell
git clone <tu repositorio> C:\Viriato ; cd C:\Viriato
Copy-Item .env.prod.example .env
```

Abre `.env` y rellénalo (el archivo explica cada cosa; **nunca se sube a git**). Lo imprescindible:

| Variable | Qué poner |
|---|---|
| `DOMAIN` | `:80` (Cloudflare pone el HTTPS). |
| `PUBLIC_URL` | La dirección pública, con `https://` (p. ej. `https://viriato.tudominio.com`). |
| `CLOUDFLARE_TUNNEL_TOKEN` | Se consigue en el paso 2. |
| `POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD` (y la misma en `ConnectionStrings__Postgres`) | Contraseñas largas y distintas. |
| `Jwt__SigningKey` | `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))` |
| `Credenciales__ClaveCifrado` | Lo mismo (32 bytes en base64). **Guárdala fuera del servidor** (gestor de contraseñas): no va en las copias de seguridad y sin ella las credenciales guardadas no se recuperan. |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | El primer administrador. Cambia la contraseña al entrar. |
| `STORAGE_HOST_PATH`, `BACKUPS_HOST_PATH` | Carpetas del PC para archivos y copias (mejor en **otro disco**), con `/` también en Windows. |

## 2 · Crear el túnel en Cloudflare

1. Cloudflare → **Zero Trust → Networks → Tunnels → Create a tunnel** → *Cloudflared* → ponle nombre (`viriato`).
2. Elige **Docker** y copia **solo el token** (la cadena larga tras `--token`) a `CLOUDFLARE_TUNNEL_TOKEN` en `.env`.
3. **Public hostname**: subdominio `viriato`, tu dominio, servicio **`HTTP`** → **`web:80`**. Guarda.

## 3 · Arrancar

```powershell
$env:APP_VERSION = (Get-Content VERSION) ; $env:GIT_SHA = (git rev-parse --short HEAD)
docker compose -f docker-compose.prod.yml --profile tunnel up -d --build
```

La primera vez construye las imágenes y **crea solo el esquema de la base de datos** y el administrador. Comprueba:

```powershell
docker compose -f docker-compose.prod.yml ps
curl https://viriato.tudominio.com/api/v1/version
```

Entra en la dirección pública con el administrador del `.env`.

## 4 · Dar de alta robots

En la plataforma: *RPA → Equipos, Servicios, Despliegues* (un despliegue por robot; al crearlo se muestra **una sola vez** su clave).
Cada robot usa `Viriato:BaseUrl = <PUBLIC_URL>` y `Viriato:ApiKey = <su clave>`. Para más capacidad, **más copias** con la misma clave
(ver [despachador-esquema.md](despachador-esquema.md)). Pon siempre un **tiempo máximo** en cada servicio.

**Los robots de BSH ya vienen en contenedores** (repositorio `bsh_p01_prices_extractor`: `docker compose up -d --build`, con su propio
`.env` de claves). En el mismo PC, `VIRIATO_URL=http://host.docker.internal:8080` si usas el compose de desarrollo, o la dirección
pública de arriba. El extractor lleva Microsoft Edge y una pantalla virtual; el creador es solo .NET. Para construirlos hace falta el token de
GitHub Packages (`read:packages`) en `GITHUB_PACKAGES_TOKEN`; no queda dentro de la imagen. Detalle en el README de ese repositorio.

## 5 · Día a día

| Tarea | Comando |
|---|---|
| Actualizar a una versión nueva | `git pull` y `docker compose -f docker-compose.prod.yml --profile tunnel up -d --build` (la base se actualiza sola al arrancar la API) |
| Ver registros | `docker compose -f docker-compose.prod.yml logs -f api` |
| Copia de seguridad ahora | `docker compose -f docker-compose.prod.yml run --rm backup now` |
| Parar todo | `docker compose -f docker-compose.prod.yml --profile tunnel down` (sin `-v`: **no borra** los datos) |

**Copias**: cada noche (`BACKUP_HOUR`) se vuelca Postgres a `BACKUPS_HOST_PATH` y, si pones `SFTP_*` (p. ej. tu hosting de Hostinger), se
sube fuera del PC junto con los documentos. Las evidencias, que pesan mucho, no se suben salvo `BACKUP_INCLUDE_EVIDENCIAS=true`.
**Haz una restauración de prueba** antes de fiarte: una copia que nunca se ha restaurado no es una copia.

## Seguridad: lo mínimo

- Cloudflare solo expone `web`; Postgres y MinIO no publican puertos en el compose de producción.
- Pon el acceso de administración detrás de **Cloudflare Access** (Zero Trust → Access → Applications) si solo lo vas a usar tú: añade una
  puerta con tu correo antes de que alguien llegue siquiera al login. Los robots necesitan una regla de *Service Auth* o excluir `/api/v1/rpa/*`.
- Cambia la contraseña del administrador inicial y no reutilices ninguna del `.env.example`.
- El PC es ahora un servidor: actualizaciones de Windows con reinicio programado fuera de horas y un SAI si es posible.

## Si falla

| Síntoma | Qué mirar |
|---|---|
| La dirección pública da error 502/1033 | `docker compose … logs cloudflared`: ¿token correcto?, ¿el *public hostname* apunta a `web:80`? |
| Carga la web pero «error de red» | `docker compose … logs api`: ¿cadena de conexión y contraseña de Postgres iguales? |
| «No se puede descifrar» en credenciales | `Credenciales__ClaveCifrado` cambió respecto a la que las cifró. |
| Tras reiniciar el PC no vuelve nada | Docker Desktop no arrancó: activa su inicio con la sesión (o inicio de sesión automático). |
