# Que Viriato y los robots estén siempre en marcha, y actualizarlos con un doble clic

```
 Se enciende el PC ─▶ Windows inicia sesión solo ─▶ Docker Desktop se abre ─▶ Docker relanza lo que estaba en marcha (restart: unless-stopped)
                                               └──▶ arrancar.ps1 espera a Docker y levanta, en orden, lo que falte (pilas.psd1)

 Doble clic en «Actualizar Viriato» ─▶ actualizar.ps1: git pull de cada repositorio ─▶ reconstruye lo que no está desplegado ─▶ resumen
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

## 3 · Docker Desktop y el token de los robots

Docker Desktop → *Settings → General*: marca **Start Docker Desktop when you sign in to your computer**.

Construir los robots necesita el token de GitHub Packages (permiso `read:packages`). Para que el botón de actualizar (paso 5) lo encuentre
sin preguntarte, guárdalo **tú** una vez como variable de tu usuario de Windows y luego cierra sesión y vuelve a entrar:

```powershell
[Environment]::SetEnvironmentVariable('GITHUB_PACKAGES_TOKEN', 'pega-aquí-tu-token', 'User')
```

Queda en tu perfil de Windows (lo puede leer cualquier programa que corra con tu usuario): usa un token que solo tenga `read:packages`.
Sin él, Viriato se actualiza igual y los robots siguen con la versión que tenían.

## 4 · Los dos accesos directos

```powershell
cd C:\Viriato ; powershell -ExecutionPolicy Bypass -File ops\arranque\instalar.ps1 -BloquearPantalla
```

Es el último comando que hace falta escribir. Deja:

- en tu carpeta *Inicio* de Windows, el **arranque al iniciar sesión** (`arrancar.ps1`, oculto);
- en el **escritorio, «Actualizar Viriato»** (`actualizar.ps1`, paso 5).

Y crea `ops\arranque\pilas.psd1`, la lista de lo que se arranca y se actualiza, y en qué orden. **Ábrelo y ajusta las carpetas** de los
robots (y `Entorno` de Viriato si tu archivo de variables no se llama `.env`). Sin `-BloquearPantalla` no bloquea nada; `instalar.ps1
-Quitar` quita los dos accesos directos. No hace falta ser administrador.

Al iniciar sesión, `arrancar.ps1`:

- abre Docker Desktop si no estaba abierto y espera hasta 10 minutos a que responda;
- hace `docker compose … up -d --no-build` de cada pila, en orden: **Viriato primero** (el extractor se une a su red para la cámara);
- usa el **mismo nombre de proyecto** que ya tenga cada compose en Docker (`docker compose ls -a`): arrancarlo con otro nombre crearía una
  segunda copia con la base de datos vacía;
- nunca construye imágenes (eso lo hace el botón de actualizar);
- si una pila falla, sigue con las demás, y lo anota todo en `ops\arranque\arranque.log`.

## 5 · Actualizar: doble clic en «Actualizar Viriato»

Cuando haya cambios subidos a GitHub (de Viriato o de los robots), doble clic en el acceso directo del escritorio. Abre una ventana que:

1. trae lo último de cada repositorio de `pilas.psd1` (`git pull`, una vez por carpeta) y te enseña los commits nuevos;
2. **reconstruye y vuelve a desplegar cada pila cuyo código no es el último desplegado**, en orden (Viriato primero). Lleva la cuenta en
   `ops\arranque\desplegado.json`, así que un build que falló se reintenta la vez siguiente aunque no haya nada nuevo. Viriato se construye
   con su versión y su commit (los ves al pie del menú);
3. se asegura de que lo demás está en marcha;
4. borra las imágenes viejas que deja cada reconstrucción (la del extractor pesa 3 GB);
5. termina con un resumen y la lista de contenedores, y espera a que pulses Intro.

Lo que conviene saber:

- **La primera vez lo reconstruye todo**: aún no sabe qué versión hay desplegada. Tarda unos minutos.
- Si algo no se puede construir (un error en el código, falta el token), **sigue funcionando la versión anterior**: el build falla antes
  de tocar ningún contenedor. La ventana lo dice en rojo.
- Solo avanza: si en este PC alguien cambió o subió algo a mano y la rama se ha separado de GitHub, no mezcla nada; avisa y deja esa
  carpeta como estaba.
- Reconstruir un robot recrea su contenedor: si estaba a mitad de un caso, ese caso lo cancela el tiempo máximo de su servicio. Mejor
  actualizar cuando los robots no estén trabajando. La API de Viriato tarda unos segundos en volver.
- Para reconstruirlo todo aunque nada haya cambiado: `powershell -ExecutionPolicy Bypass -File C:\Viriato\ops\arranque\actualizar.ps1 -Todo`.

## 6 · La prueba de verdad

Doble clic en «Actualizar Viriato» y espera al resumen (la primera vez lo construye todo). Después **reinicia el PC y no toques nada.** A los
2–3 minutos, desde el móvil **con datos** (no con el Wi-Fi de casa): abre `https://app.viriato.org` y entra. En el PC, `docker ps` debe
mostrarlo todo `Up` y `arranque.log` terminar en `Hecho: 4 de 4 en marcha.`

## Día a día

| Quiero… | Haz |
|---|---|
| Desplegar lo último | Doble clic en «Actualizar Viriato» (escritorio). |
| Parar un robot un rato | `docker compose stop extractor` en su carpeta. Vuelve en el próximo inicio de sesión. |
| Que algo deje de arrancar y de actualizarse solo | Quítalo de `pilas.psd1` **y** `docker compose down` en su carpeta (sin `-v`: `-v` borra sus datos). |
| Saber qué pasó al arrancar o al actualizar | `ops\arranque\arranque.log` |

## Si falla

| Síntoma | Qué mirar |
|---|---|
| Tras reiniciar, el PC está en la pantalla de contraseña | El inicio de sesión automático (paso 2). |
| `arranque.log` no existe o no tiene la hora del último inicio | El acceso directo: ¿está en `shell:startup` (Win+R)? Vuelve a pasar `instalar.ps1`. |
| «Docker no ha respondido en 10 minutos» | Abre Docker Desktop a mano y mira su error; a menudo lo arregla `wsl --update`. |
| «No such image» / «pull access denied» al arrancar | Esa pila nunca se construyó en este PC: doble clic en «Actualizar Viriato». |
| Viriato no arranca: «port is already allocated» o «socket no permitido» (puerto 80) | Con el túnel el 80 no hace falta: en el `.env` de Viriato, `HTTP_PORT=8088` y `HTTPS_PORT=8443`, y otra vez «Actualizar Viriato». |
| «network … not found» | Ya lo arregla solo (vuelve a crear los contenedores sin tocar los datos). A mano: `docker compose … up -d --no-build --force-recreate`. |
| Al actualizar: «No se pudo traer lo nuevo» | En esa carpeta, `git status`: cambios hechos a mano en el servidor o rama separada de GitHub. |
| Al actualizar: «falta el token de GitHub Packages» | El paso 3 (y cerrar sesión y volver a entrar después de guardarlo). |
| «network viriato declared as external, but could not be found» | Viriato no arrancó (mira sus líneas más arriba) o es anterior al nombre fijo de red: actualízalo, o pon su red en `VIRIATO_RED` en el `.env` de los robots. |
| La web sale vacía, como recién instalada | Viriato se arrancó con otro nombre de proyecto. `docker compose ls -a`: pon en `Proyecto` (en `pilas.psd1`) el que tiene tus datos y para el otro con `docker compose -p <nombre> down` (**sin** `-v`). |
