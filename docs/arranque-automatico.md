# Que Viriato y los robots estén siempre en marcha, también después de reiniciar

```
 Se enciende el PC ─▶ Windows inicia sesión solo ─▶ Docker Desktop se abre ─▶ Docker relanza lo que estaba en marcha (restart: unless-stopped)
                                               └──▶ arrancar.ps1 espera a Docker y levanta, en orden, lo que falte (pilas.psd1)
```

Docker ya vuelve a encender por sí solo los contenedores que estaban en marcha: todos los de Viriato y de los robots llevan
`restart: unless-stopped`. Lo que falla en la práctica es lo de alrededor: el PC se queda en la pantalla de contraseña tras un reinicio,
Docker Desktop no se abre, o algo se paró a mano y ya no vuelve. Esta guía cierra esos huecos. Todo se hace **en el PC que hace de
servidor** (los ejemplos suponen Viriato en `C:\Viriato`).

## 1 · Que el PC no se apague ni se duerma

1. **Sin suspensión**: Configuración → Sistema → Energía → *Suspender cuando esté enchufado*: **Nunca** (que se apague la pantalla da igual).
   O, en PowerShell:
   ```powershell
   powercfg /change standby-timeout-ac 0
   ```
   ```powershell
   powercfg /change hibernate-timeout-ac 0
   ```
2. **Que se encienda solo tras un corte de luz** (recomendado): en la BIOS/UEFI, la opción *Restore on AC Power Loss* / *After Power Failure*
   (en los HP, *Power-on after power loss*) en **Power On**. Cada placa la llama de una forma; si no la tiene, un SAI.

## 2 · Que Windows inicie sesión solo

Docker Desktop solo funciona **dentro de una sesión de usuario**. Tras un reinicio (una actualización de Windows a las 3 de la mañana, un
corte de luz) el PC se queda en la pantalla de contraseña y no arranca nada hasta que alguien entra. Hay que quitar ese paso:

1. Si entras con PIN o huella: Configuración → Cuentas → Opciones de inicio de sesión → desactiva *«Para mejorar la seguridad, permitir
   solo el inicio de sesión de Windows Hello para las cuentas Microsoft»* (si no, Windows no acepta la contraseña para entrar solo).
2. Descarga **Autologon**, de Microsoft Sysinternals (`learn.microsoft.com/sysinternals/downloads/autologon`), ábrelo y pon tu usuario
   (con cuenta Microsoft, el correo), el nombre del PC y tu contraseña → **Enable**. Guarda la contraseña cifrada, no en texto.
   (Alternativa sin descargar nada: `netplwiz` → quita *«Los usuarios deben escribir su nombre y contraseña»*.)

Ojo: con esto, quien tenga el PC delante entra sin contraseña. Por eso el paso 4 puede **bloquear la pantalla** en cuanto todo está en
marcha (Docker sigue funcionando con la sesión bloqueada).

## 3 · Docker Desktop

Docker Desktop → *Settings → General*: marca **Start Docker Desktop when you sign in to your computer**. Y deja construidas y en marcha,
una vez, todas las pilas (la primera vez se hace a mano porque construir los robots necesita el token de GitHub Packages):

```powershell
cd C:\Viriato ; docker compose -f docker-compose.prod.yml --profile tunnel up -d --build
```

y lo equivalente en cada repositorio de robots (su README). `restart: unless-stopped` solo devuelve lo que **estaba** en marcha.

## 4 · El arranque al iniciar sesión

```powershell
cd C:\Viriato ; powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -BloquearPantalla
```

Deja un acceso directo en tu carpeta *Inicio* de Windows (no hace falta ser administrador) y crea `ops\arranque\pilas.psd1`, la lista de
lo que se arranca y en qué orden. **Ábrelo y ajusta las carpetas** de los robots (y `Entorno` de Viriato si tu archivo de variables no se
llama `.env`). Sin `-BloquearPantalla` no bloquea nada; `instalar.ps1 -Quitar` lo quita.

Al iniciar sesión, `arrancar.ps1`:

- abre Docker Desktop si no estaba abierto y espera hasta 10 minutos a que responda;
- hace `docker compose … up -d --no-build` de cada pila, en orden: **Viriato primero** (el extractor se une a su red para la cámara);
- usa el **mismo nombre de proyecto** que ya tenga cada compose en Docker (`docker compose ls -a`): arrancarlo con otro nombre crearía una
  segunda copia con la base de datos vacía;
- nunca construye imágenes (eso es a mano, al actualizar);
- si una pila falla, sigue con las demás, y lo anota todo en `ops\arranque\arranque.log`.

Pruébalo ya, sin reiniciar:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Viriato\ops\arranque\arrancar.ps1
```

El registro debe terminar en `Hecho: 4 de 4 en marcha.`

## 5 · La prueba de verdad

**Reinicia el PC y no toques nada.** A los 2–3 minutos, desde el móvil **con datos** (no con el Wi-Fi de casa): abre `https://app.viriato.org`
y entra. En el PC, `docker ps` debe mostrarlo todo `Up` y `arranque.log` terminar en `Hecho`.

## Día a día

| Quiero… | Haz |
|---|---|
| Parar un robot un rato | `docker compose stop extractor` en su carpeta. Vuelve en el próximo inicio de sesión. |
| Que algo deje de arrancar solo | Quítalo de `pilas.psd1` **y** `docker compose down` en su carpeta (sin `-v`: `-v` borra sus datos). |
| Actualizar | `git pull` y `docker compose … up -d --build` a mano: el arranque nunca construye. |
| Saber qué pasó al arrancar | `ops\arranque\arranque.log` |

## Si falla

| Síntoma | Qué mirar |
|---|---|
| Tras reiniciar, el PC está en la pantalla de contraseña | El inicio de sesión automático (paso 2). |
| `arranque.log` no existe o no tiene la hora del último inicio | El acceso directo: ¿está en `shell:startup` (Win+R)? Vuelve a pasar `instalar.ps1`. |
| «Docker no ha respondido en 10 minutos» | Abre Docker Desktop a mano y mira su error; a menudo lo arregla `wsl --update`. |
| «No such image» / «pull access denied» | Esa pila nunca se construyó en este PC: una vez a mano con `up -d --build`. |
| «network viriato declared as external, but could not be found» | Viriato no arrancó (mira sus líneas más arriba) o es anterior al nombre fijo de red: actualízalo, o pon su red en `VIRIATO_RED` en el `.env` de los robots. |
| La web sale vacía, como recién instalada | Viriato se arrancó con otro nombre de proyecto. `docker compose ls -a`: pon en `Proyecto` (en `pilas.psd1`) el que tiene tus datos y para el otro con `docker compose -p <nombre> down` (**sin** `-v`). |
