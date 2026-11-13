# El lanzador de robots: un contenedor para todos, y cada robot solo mientras tiene trabajo

Hasta ahora cada robot era un contenedor siempre encendido que preguntaba «¿tienes algo para mí?» cada pocos segundos. Con un
robot ligero no importa; con uno que lleva Edge y una pantalla virtual, cada robot ocupa memoria todo el día para trabajar un rato.

El **lanzador** es **un solo contenedor por máquina**. Los robots **no están arrancados**: el lanzador mira en Viriato qué robots
tienen trabajo esperando, arranca el que toca como un proceso, y el robot se apaga solo cuando vacía su cola.

**En el servidor de Viriato el lanzador ya viene con Viriato** (`docker-compose.prod.yml`) y **no se configura nada**: los robots se
suben en Viriato y él los descarga. Ni `.env`, ni claves, ni carpetas. Ver [Desplegar un robot](#desplegar-un-robot-subir-su-zip).

```
  Viriato (API) ── decide qué caso le toca a quién, como siempre (docs/despachador-esquema.md)
        ▲   ▲
        │   └── «dame el siguiente»  ← lo pide el propio robot, mientras está arrancado
        │ «¿cuánto hay pendiente para este robot?»  /  «esto es lo que estoy haciendo con él»
        │
  Lanzador (1 contenedor)
        ├─ bsh-creador      proceso, solo mientras tiene casos
        ├─ bsh-extractor    proceso + Edge + su pantalla virtual, solo mientras tiene casos
        └─ …                en reposo no corre ninguno
```

## Desplegar un robot: subir su .zip

1. Publica el robot y comprímelo. Vale el `.zip` de la carpeta o de lo que hay dentro:

   ```powershell
   dotnet publish src\Bsh.PricesExtractor -c Release -o publicado\bsh-extractor
   Compress-Archive publicado\bsh-extractor\* bsh-extractor.zip
   ```

2. En Viriato, *RPA → Servicios*, en la fila del servicio, el botón **Subir** / **Versiones** → arrastra el `.zip` a la caja (o
   **Elegir archivo**). El programa es del servicio: lo comparten todos sus despliegues.
3. La primera vez, en *RPA → Despliegues*, abre el despliegue → pestaña **Robot** → activa **Lo arranca el lanzador de Viriato**.

Ya está. En la siguiente ronda (unos segundos) el lanzador descarga la versión, comprueba que es lo que se subió y la usa la próxima
copia que arranque; la que esté trabajando termina con la que tenía. Cada subida es una versión nueva del servicio (1, 2, 3…) y llega
sola a todos sus despliegues en **Siempre la última**. Para volver atrás (o probar una versión en un solo equipo), en la pestaña
**Robot** del despliegue elige **Fijar en la versión N**. Una versión en uso no se puede borrar. Desactivar **Lo arranca el lanzador**
hace que el lanzador deje en paz ese despliegue (porque el robot corre en otra máquina, con su clave). La columna *Robot* de la lista
de despliegues dice qué versión corre (o «Fuera», o «Sin versión» si el servicio aún no tiene ninguna).

**Cómo sabe ejecutarlo.** Lo deduce del `.zip` al subirlo (si no puede, no lo acepta y dice por qué):

| Mira | Deduce |
|---|---|
| El `*.runtimeconfig.json` que hay arriba del todo | Se ejecuta con `dotnet Nombre.dll`. |
| Si lleva `Viriato.Rpa.Client.Selenium.dll`, `WebDriver.dll` o `Microsoft.Playwright.dll` | Abre un navegador: le da su pantalla virtual y un tope de memoria de 2048 MB. |
| Un `robot.json` arriba del todo (opcional) | Lo que no se puede deducir: `{"comando", "argumentos", "pantalla", "memoriaMaximaMb", "entorno"}`, todo opcional. Las réplicas no van aquí: son del despliegue. Para un robot que no es .NET, que tiene varios programas o que necesita ajustes propios. |

**Las claves.** El lanzador no usa la clave del despliegue. La API de Viriato crea al arrancar un secreto en un volumen que solo montan
ella y el lanzador (`lanzador_secreto`); con él, el lanzador lee su catálogo y recibe para cada robot un **pase** firmado
(`rpl_…`, válido 7 días, se renueva en cada ronda) que el robot usa como clave. Nadie lo escribe ni lo copia. Para anular todos los pases
a la vez: borra ese volumen y reinicia la API y el lanzador. Quien controle Docker en el servidor puede leer el secreto, igual que ya
puede leer el `.env` de Viriato y la base de datos: no se abre ninguna puerta nueva.

**Cuánto corre a la vez se decide en Viriato**, sin tocar el servidor (el lanzador lo lee en cada ronda):

| Dónde | Qué | Por defecto |
|---|---|---|
| *Despliegues* → botón **Lanzador** | Robots a la vez en la máquina, y cuántos de ellos con navegador | 2 y 1 |
| Despliegue → pestaña **Réplicas** | Cuántas copias de ese robot a la vez cuando hay trabajo (0 lo aparca). Es del despliegue, no del robot: subir otra versión no la cambia | 1 |
| Despliegue → pestaña **Réplicas** | Memoria máxima de cada réplica | 2048 MB con navegador; sin tope sin él |

Para tener **dos extractores a la vez**: «Réplicas» 2 en el extractor y, en «Lanzador», al menos 2 con navegador. Cada Edge
necesita unos 2 GB: mira que el PC (y la memoria que Docker Desktop tiene asignada) los tenga. Y mira el **límite global** del servicio:
si el portal no admite dos sesiones con la misma cuenta, déjalo en 1. El contenedor no tiene tope propio de memoria ni CPU: lo protege el
tope de cada copia.

**Lo demás que el robot necesita** lo pone Viriato: dónde se ve su pantalla (`Vista__UrlPublica`, a partir de `PUBLIC_URL`), y la
vista en directo y el vídeo, en la pestaña *Vista en directo* del despliegue.

Los zips se guardan en `STORAGE_HOST_PATH/robots`, así que la copia de cada noche se los lleva.

## Qué decide y qué no

- **Decide cuándo hay un robot arrancado.** Nada más.
- **No decide qué caso hace cada robot.** Eso sigue en Viriato, con su cerrojo, las prioridades, los límites por servicio y el
  orden del equipo. El robot que arranca el lanzador pide trabajo igual que uno que estuviera siempre encendido.

Moverlo todo fuera de la API no ahorraría memoria (la decisión es una consulta a la base de datos dentro de una API que ya está
encendida) y obligaría a coordinar dos sitios para no dar el mismo caso dos veces.

## Las reglas del lanzador

Cada 5 segundos, para cada robot subido a Viriato:

1. **Pregunta a Viriato** cuánto tiene esperando y cuánto en ejecución (`GET /api/v1/rpa/lanzador/trabajo`, con la clave del robot).
2. **Arranca las copias que faltan**: una por cada paso que tenga (en marcha o esperando), hasta sus **réplicas**. Una copia ya
   arrancada y libre cuenta primero: no se arranca otra mientras esa pueda coger el trabajo.
3. **Respeta los topes de la máquina**: robots a la vez y, de ellos, con navegador (los que pesan),
   los dos en el botón **Lanzador**. Si no caben todos, **manda el orden en que se crearon los despliegues**.
4. **No arranca** un robot apagado en Viriato, uno cuyo servicio está en su límite global, ni uno que acaba de fallar (espera
   `esperaTrasFalloSegundos`, el doble tras cada fallo seguido, hasta 15 minutos).
5. **No para a nadie que esté trabajando.** Cada copia se apaga sola tras 30 segundos sin recibir un paso.
6. **Cuenta a Viriato lo que hace** (`PUT /api/v1/rpa/lanzador/estado`), para que se vea en pantalla.

Y vigila lo que tiene arrancado:

| Qué pasa | Qué hace |
|---|---|
| Una copia termina con error | No la relanza enseguida (regla 4) y lo cuenta: «No arranca». |
| Una copia usa más memoria que su `memoriaMaximaMb` (ella y todo lo que abrió: el navegador) | La mata y lo cuenta como fallo. |
| Viriato dice que el robot no tiene nada, y la copia sigue ahí pasados esos 30 segundos y un minuto más | La para: está colgada, o usa un cliente anterior al 0.12, que no sabe apagarse. |
| Hay trabajo esperando, hay copias, y ninguna trabaja durante `reiniciarSiNoCogeTrabajoSegundos` (5 min; 0 = nunca) | Las reinicia. No si es Viriato quien retiene el trabajo (servicio al límite, despliegue apagado). |
| Una copia arranca, Viriato no le da nada (no era su turno) y se apaga | No la relanza enseguida; lo cuenta como «esperando», no como fallo. |
| Viriato no contesta | Lo que corre sigue corriendo; no se arranca nada nuevo hasta que conteste. |

Al parar el contenedor (`docker compose stop`) cada robot recibe la señal de cierre y tiene `graciaAlPararSegundos` para terminar
(o dar por fallido) el paso que lleva.

## Lo que recibe cada robot

El lanzador arranca cada copia como un proceso aparte, en su propia sesión, con estas variables:

| Variable | Valor |
|---|---|
| `Viriato__BaseUrl`, `Viriato__ApiKey` | Dónde está Viriato y el pase **de ese robot** (nunca ve el secreto del lanzador: las variables `LANZADOR_*` se quitan de su entorno). |
| `Worker__SalirSinTrabajo` | `00:00:30`: con `Viriato.Rpa.Client` **0.12+** el robot se apaga solo. |
| `TMPDIR` | Una carpeta temporal propia, que se borra cuando la copia termina. |
| `DISPLAY`, `VistaEnDirecto__PuertoWeb`, `VistaEnDirecto__PuertoVnc` | Solo con `"pantalla": true`: **una pantalla virtual propia** (Xvfb + openbox) y sus puertos. La primera copia con pantalla usa `:99` y el 6080; la segunda, `:100` y el 6081. Dos navegadores nunca comparten pantalla. |
| Lo de `"entorno"` de su `robot.json` | Los ajustes propios del robot. Un valor puede usar `{puertoWeb}`, `{puertoVnc}`, `{pantalla}`. |

## Verlo en Viriato

*RPA → Equipos → Despacho* de la máquina, en «Robots y sus copias»:

| Se ve | Significa |
|---|---|
| **Sin trabajo: arrancará solo** | No hay nada esperando y no hay ninguna copia. Es lo normal en reposo. |
| **En marcha** (o «2 de 3 copias en marcha») | Hay copias trabajando. Debajo, si quiere más y no caben, por qué. |
| **Esperando para arrancar** + el motivo | Hay trabajo pero aún no cabe: «el lanzador ya tiene 2 de 2 robots en marcha», «ya hay 1 de 1 robots con navegador», «el servicio está en su límite global»… |
| **No arranca** + el motivo | No se pudo ejecutar, o se cae al arrancar. Dice cuándo se reintenta. |
| **Apagado** | El despliegue está apagado en Viriato. |

En «Esperando, en este orden», un paso cuyo robot no está arrancado ya no dice «Robot sin conexión» si lo lleva un lanzador: dice
«Arrancando el robot», «Esperando para arrancar» o «El robot no arranca».

Si el lanzador deja de dar señales 90 segundos, Viriato deja de enseñar lo que dijo y el robot vuelve a verse como uno normal
(«Sin copias en marcha»).

## Cómo encaja con el despacho de Viriato

- Las preguntas del lanzador **no cuentan como que el robot esté conectado** (van con la cabecera `X-Viriato-Lanzador`). Si
  contaran, un robot apagado parecería una copia libre y, en una máquina con tope, el despachador se quedaría esperándolo.
- En una máquina **con tope de ejecuciones** en Viriato (RPA → Equipos → Despacho), Viriato solo reparte entre los robots que están
  arrancados. Lo más simple es dejar el equipo **sin tope** y poner el tope en el lanzador (botón **Lanzador**), que es quien sabe
  cuánta memoria hay.
- **Límite conocido:** con el lanzador en su tope, un robot no suelta su hueco hasta vaciar su cola. Si el primer despliegue
  tiene siempre trabajo, los de después esperan. El orden de creación es la prioridad; no hay turnos entre robots.

## Lo que cuesta

- **Arranque en frío:** entre que entra un caso y un robot parado empieza hay que sumar hasta 5 segundos más lo que tarde
  el robot en arrancar. Medido en un Mac con Docker, con el robot de ejemplo (.NET, sin navegador) y una ronda cada 3 s:
  **entre 1,1 y 3,4 s** desde que se crea el caso hasta que el robot lo coge (tres medidas). Un robot con Edge tardará más;
  **falta medirlo en el servidor** con el extractor.
- **Memoria, medida en ese mismo Mac:** el lanzador en reposo, sin robots, **26–31 MB y 0,05 % de CPU**; con un robot .NET ligero
  trabajando, ~52 MB. Falta comparar en el servidor con los contenedores actuales (`docker stats --no-stream`).
- **Un único punto de fallo:** si el lanzador se cae no arranca ningún robot. `restart: unless-stopped` lo levanta, y al volver
  empieza de cero (los robots que tuviera se pararon con él).
- **Menos aislamiento:** todos los robots comparten contenedor. Cada copia tiene su sesión, su carpeta temporal, su pantalla y su
  tope de memoria, y no ve las claves de los demás; pero comparten sistema de archivos y usuario.

## Para quien lo mantiene

- Código: `backend/sdk/Viriato.Rpa.Lanzador` (`Planificador` son las reglas, puras y con tests; `LanzadorDeRobots` es la ronda;
  `ArrancadorDeProcesos` arranca, mide y para). Tests: `backend/tests/Viriato.Rpa.Lanzador.Tests` y `LanzadorFlowTests` en los de
  integración.
- API: `LanzadorEndpoints` (módulo Casos): lo que pregunta con la clave de un robot (`/trabajo`, `/estado`) y, con el secreto,
  `/catalogo` y `/versiones/{id}/paquete`. El estado se guarda en el despliegue (`lanzador_*`, migración `LanzadorDeRobots`).
- Junto a Viriato: `SecretoDelLanzador` (el secreto y los pases), `PaqueteDeRobot` (qué se deduce del `.zip`), `AlmacenDeRobots` y
  `VersionesDeRobotEndpoints` en el módulo RpaFleet (migraciones `VersionesDeRobot` y `LasVersionesSonDelServicio`: las versiones
  cuelgan del servicio, y el despliegue guarda `en_el_lanzador` y, si está fijado, `version_de_robot_id`); en el lanzador, `CatalogoDeViriato` (descarga y
  desempaqueta cada versión una vez, en `/var/lib/lanzador/robots`). Réplicas y memoria, en el despliegue; topes de la máquina, en
  `AjustesDelLanzador` (migraciones `ReplicasYAjustesDelLanzador` y `LasReplicasSonDelDespliegue`).
- Robots: `Worker:SalirSinTrabajo` en `Viriato.Rpa.Client` 0.12.0. Un robot con un cliente anterior funciona, pero no se apaga
  solo: el lanzador lo para cuando Viriato dice que no tiene nada (y si justo entra un caso en ese instante, ese paso falla y se
  reprocesa). Súbelos a 0.12.
