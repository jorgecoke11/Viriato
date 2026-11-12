# El lanzador de robots: un contenedor para todos, y cada robot solo mientras tiene trabajo

Hasta ahora cada robot era un contenedor siempre encendido que preguntaba «¿tienes algo para mí?» cada pocos segundos. Con un
robot ligero no importa; con uno que lleva Edge y una pantalla virtual, cada robot ocupa memoria todo el día para trabajar un rato.

El **lanzador** es **un solo contenedor por máquina**. Los robots viven como programas en una carpeta del servidor y **no están
arrancados**: el lanzador mira en Viriato qué robots tienen trabajo esperando, arranca el que toca como un proceso, y el robot se
apaga solo cuando vacía su cola.

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

## Qué decide y qué no

- **Decide cuándo hay un robot arrancado.** Nada más.
- **No decide qué caso hace cada robot.** Eso sigue en Viriato, con su cerrojo, las prioridades, los límites por servicio y el
  orden del equipo. El robot que arranca el lanzador pide trabajo igual que uno que estuviera siempre encendido.

Moverlo todo fuera de la API no ahorraría memoria (la decisión es una consulta a la base de datos dentro de una API que ya está
encendida) y obligaría a coordinar dos sitios para no dar el mismo caso dos veces.

## Las reglas del lanzador

Cada `intervaloSegundos` (5 por defecto), para cada robot del catálogo:

1. **Pregunta a Viriato** cuánto tiene esperando y cuánto en ejecución (`GET /api/v1/rpa/lanzador/trabajo`, con la clave del robot).
2. **Arranca las copias que faltan**: una por cada paso que tenga (en marcha o esperando), hasta su `maxCopias`. Una copia ya
   arrancada y libre cuenta primero: no se arranca otra mientras esa pueda coger el trabajo.
3. **Respeta los topes de la máquina**: `maxRobotsALaVez` (copias de cualquier robot a la vez) y `maxConPantalla` (las que llevan
   navegador, que son las que pesan). Si no caben todos, **manda el orden del catálogo**.
4. **No arranca** un robot apagado en Viriato, uno cuyo servicio está en su límite global, ni uno que acaba de fallar (espera
   `esperaTrasFalloSegundos`, el doble tras cada fallo seguido, hasta 15 minutos).
5. **No para a nadie que esté trabajando.** Cada copia se apaga sola tras `salirSinTrabajoSegundos` sin recibir un paso.
6. **Cuenta a Viriato lo que hace** (`PUT /api/v1/rpa/lanzador/estado`), para que se vea en pantalla.

Y vigila lo que tiene arrancado:

| Qué pasa | Qué hace |
|---|---|
| Una copia termina con error | No la relanza enseguida (regla 4) y lo cuenta: «No arranca». |
| Una copia usa más memoria que su `memoriaMaximaMb` (ella y todo lo que abrió: el navegador) | La mata y lo cuenta como fallo. |
| Viriato dice que el robot no tiene nada, y la copia sigue ahí pasados `salirSinTrabajoSegundos` + `graciaParaSalirSegundos` | La para: está colgada, o usa un cliente anterior al 0.12, que no sabe apagarse. |
| Hay trabajo esperando, hay copias, y ninguna trabaja durante `reiniciarSiNoCogeTrabajoSegundos` (5 min; 0 = nunca) | Las reinicia. No si es Viriato quien retiene el trabajo (servicio al límite, despliegue apagado). |
| Una copia arranca, Viriato no le da nada (no era su turno) y se apaga | No la relanza enseguida; lo cuenta como «esperando», no como fallo. |
| Viriato no contesta | Lo que corre sigue corriendo; no se arranca nada nuevo hasta que conteste. |

Al parar el contenedor (`docker compose stop`) cada robot recibe la señal de cierre y tiene `graciaAlPararSegundos` para terminar
(o dar por fallido) el paso que lleva.

## Lo que recibe cada robot

El lanzador arranca cada copia como un proceso aparte, en su propia sesión, con estas variables:

| Variable | Valor |
|---|---|
| `Viriato__BaseUrl`, `Viriato__ApiKey` | Dónde está Viriato y la clave **de ese robot** (nunca la de otro: se quitan de su entorno). |
| `Worker__SalirSinTrabajo` | `00:00:30` (lo de `salirSinTrabajoSegundos`): con `Viriato.Rpa.Client` **0.12+** el robot se apaga solo. |
| `TMPDIR` | Una carpeta temporal propia, que se borra cuando la copia termina. |
| `DISPLAY`, `VistaEnDirecto__PuertoWeb`, `VistaEnDirecto__PuertoVnc` | Solo con `"pantalla": true`: **una pantalla virtual propia** (Xvfb + openbox) y sus puertos. La primera copia con pantalla usa `:99` y el 6080; la segunda, `:100` y el 6081. Dos navegadores nunca comparten pantalla. |
| Lo de `"entorno"` | Los ajustes propios del robot. Un valor puede usar `${VARIABLE}` (del lanzador) y `{puertoWeb}`, `{puertoVnc}`, `{pantalla}`. |

## El catálogo (`robots.json`)

Está en la carpeta de robots del servidor, junto a una subcarpeta por robot. **No lleva secretos**: cada robot nombra la variable
de entorno donde está su clave. Se relee solo cuando cambia: añadir un robot no necesita reiniciar el lanzador.

```json
{
  "maxRobotsALaVez": 2,
  "maxConPantalla": 1,
  "salirSinTrabajoSegundos": 30,
  "robots": [
    {
      "nombre": "bsh-creador",
      "claveEnv": "CREADOR_API_KEY",
      "comando": "dotnet",
      "argumentos": ["/robots/bsh-creador/Bsh.CasesCreator.dll"],
      "directorio": "/robots/bsh-creador"
    },
    {
      "nombre": "bsh-extractor",
      "claveEnv": "EXTRACTOR_API_KEY",
      "comando": "dotnet",
      "argumentos": ["/robots/bsh-extractor/Bsh.PricesExtractor.dll"],
      "directorio": "/robots/bsh-extractor",
      "pantalla": true,
      "memoriaMaximaMb": 1800,
      "entorno": { "Tradeplace__Credencial": "${TRADEPLACE_CREDENCIAL}", "Vista__UrlPublica": "${VISTA_URL_PUBLICA}" }
    }
  ]
}
```

| Del lanzador | Por defecto | |
|---|---|---|
| `viriatoUrl` | — | Mejor en la variable `VIRIATO_URL`, que gana. |
| `intervaloSegundos` | 5 | Cada cuánto mira. |
| `maxRobotsALaVez` | 2 | Tope de copias a la vez en la máquina. |
| `maxConPantalla` | 1 | De esas, cuántas con navegador. 0 = este lanzador no lleva navegador. |
| `salirSinTrabajoSegundos` | 30 | Cuánto espera un robot sin trabajo antes de apagarse. Más alto = menos arranques si los casos llegan a ráfagas. |
| `esperaTrasFalloSegundos` | 60 | Ver regla 4. |
| `graciaParaSalirSegundos`, `reiniciarSiNoCogeTrabajoSegundos`, `graciaAlPararSegundos` | 60, 300, 30 | Ver la tabla de vigilancia. |

| De cada robot | |
|---|---|
| `nombre` | Para el registro y su carpeta temporal. Único. |
| `claveEnv` | Nombre de la variable con la clave de su despliegue. |
| `comando`, `argumentos`, `directorio` | Cómo se ejecuta. No tiene que ser .NET: vale cualquier programa. |
| `maxCopias` | 1 por defecto. 0 = aparcado: no se arranca. |
| `pantalla` | `true` si abre un navegador. |
| `memoriaMaximaMb` | Tope de memoria de una copia. Vacío: sin tope. |
| `entorno` | Sus ajustes propios. |

Si el catálogo tiene un error (un robot sin clave, un nombre repetido…), el lanzador lo lista todo en su registro y **no arranca
nada** hasta que se corrija.

## Ponerlo en marcha

Todo está en `ops/lanzador/`.

```powershell
cd ops\lanzador
Copy-Item .env.example .env                        # las claves de los robots y dónde está Viriato
mkdir robots ; Copy-Item robots.example.json robots\robots.json
```

1. **Publica cada robot en su carpeta** (una vez, y cada vez que cambie). Para los de BSH, desde su repositorio, con el token de
   GitHub Packages en `GITHUB_PACKAGES_TOKEN`:

   ```powershell
   dotnet publish src\Bsh.CasesCreator   -c Release -o <ROBOTS_DIR>\bsh-creador
   dotnet publish src\Bsh.PricesExtractor -c Release -o <ROBOTS_DIR>\bsh-extractor
   ```

2. **Elige la imagen** en `.env`: `LANZADOR_IMAGEN=ligero` (solo .NET, ~320 MB) o `navegador` (además Edge, Xvfb, openbox,
   x11vnc/noVNC y ffmpeg, ~3 GB en disco; solo `linux/amd64`). Con algún robot de `"pantalla": true` hace falta `navegador`.
3. **Pon las claves** en `.env` (una variable por robot, la de su `claveEnv`) y `VIRIATO_URL`.
4. `docker compose up -d --build` y `docker compose logs -f`. **En el servidor de Viriato**, súmale la red de Viriato:
   `docker compose -f docker-compose.yml -f docker-compose.red-viriato.yml up -d --build`. Así los robots llegan a la API por
   dentro (`http://api:8080`) y la vista en directo pública (`/vista/`) sigue funcionando: el lanzador responde en esa red con el
   nombre `extractor`, el que usaba el contenedor suelto. Pon `VISTA_URL_PUBLICA=https://<tu dominio>/vista/vnc.html?path=vista/websockify`.

`LANZADOR_MEMORIA` y `LANZADOR_CPUS` son el tope **del contenedor entero**: el lanzador más los robots que tenga arrancados a la
vez. Con `maxConPantalla: 1`, el tope real de memoria es un navegador, no uno por robot.

**No arranques a la vez el lanzador y los contenedores sueltos de los mismos robots**: serían dos copias con la misma clave.

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
  arrancados. Lo más simple es dejar el equipo **sin tope** y poner el tope en el lanzador (`maxRobotsALaVez`), que es quien sabe
  cuánta memoria hay.
- **Límite conocido:** con el lanzador en su tope, un robot no suelta su hueco hasta vaciar su cola. Si el primero del catálogo
  tiene siempre trabajo, los de abajo esperan. El orden del catálogo es la prioridad; no hay turnos entre robots.

## Lo que cuesta

- **Arranque en frío:** entre que entra un caso y un robot parado empieza hay que sumar hasta `intervaloSegundos` más lo que tarde
  el robot en arrancar. Medido en un Mac con Docker, con el robot de ejemplo (.NET, sin navegador) y `intervaloSegundos: 3`:
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
- API: `LanzadorEndpoints` (módulo Casos). El estado se guarda en el despliegue (`lanzador_*`, migración `LanzadorDeRobots`).
- Robots: `Worker:SalirSinTrabajo` en `Viriato.Rpa.Client` 0.12.0. Un robot con un cliente anterior funciona, pero no se apaga
  solo: el lanzador lo para cuando Viriato dice que no tiene nada (y si justo entra un caso en ese instante, ese paso falla y se
  reprocesa). Súbelos a 0.12.
