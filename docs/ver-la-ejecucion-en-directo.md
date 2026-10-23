# Seguir un caso mientras el robot lo ejecuta: en directo, con porcentaje y en vídeo

Tres cosas, todas desde el caso en Viriato:

1. **La pantalla del robot, dentro del caso** 🎥: mientras un robot ejecuta un caso, su página muestra un panel **En directo** con la pantalla del robot
   (como un Selenium Hub, pero dentro de Viriato). En el Listado, ese caso lleva una cámara que abre la misma pantalla en una ventana.
2. **El porcentaje**: una barra con cuánto lleva y qué está haciendo («Añadiendo productos a la cesta (14 de 48)»), en la página del caso y en
   el Listado.
3. **El vídeo**: cada ejecución del extractor queda grabada y se sube al caso como evidencia de tipo vídeo (aparece en el historial, filtro *Vídeos*).

## Cómo funciona

```
 contenedor del robot                                       Viriato
 ┌────────────────────────────────────────────┐   POST /api/v1/rpa/pasos/{id}/en-vivo
 │ Xvfb (pantalla virtual) ── Edge (Selenium)  │ ──{ porcentaje, mensaje, vistaUrl }──▶ guarda lo último
 │      │            │                         │
 │   x11vnc ─ noVNC :6080   ffmpeg (4 fps) ────┼──▶ al acabar: sube el .mp4 como evidencia del caso
 └──────┼─────────────────────────────────────┘
        └── http://localhost:6080/vnc.html  ◀── la cámara abre esta dirección (solo se mira, no se toca)
```

- **No hace falta un Selenium Hub.** El Hub reparte sesiones de navegador entre máquinas; aquí cada robot lleva su propio navegador y lo único que
  se quiere es *mirar su pantalla*. Eso lo da noVNC (x11vnc + websockify) dentro del propio contenedor.
- El robot dice **dónde** se ve su pantalla (`vistaUrl`) al empezar cada caso. Viriato solo acepta direcciones `http`/`https` (nunca `javascript:`),
  y el front lo vuelve a comprobar antes de enlazarla.
- El avance **es lo que el robot cuenta**: Viriato no lo adivina. Se guarda el último dato (`ProgresoPorcentaje`, `ProgresoMensaje`) mientras el paso
  corre y desaparece cuando termina. No entra en el historial del caso: el historial queda como era.

## Quién decide la vista y el vídeo: la plataforma, por despliegue

Si un robot se ve en directo, con qué calidad, y si graba vídeo **no lo decide cada robot**: se elige en **Despliegues → Pantalla del robot**
(el icono del monitor de cada fila). La tabla muestra la calidad de cada uno y un icono de cámara si el vídeo está forzado.

| Vista | Qué es | Imagen cada | Calidad / compresión del visor |
|---|---|---|---|
| Apagada | Nadie la ve; el robot no la sirve | — | — |
| **Baja** (la de partida) | La que menos CPU le quita al robot | 500 ms | 2 / 9 |
| Media | | 200 ms | 5 / 6 |
| Alta | Como era antes | 20 ms | 6 / 2 |

Vídeo de cada ejecución: *Lo decide el robot* (lo de siempre, `Grabacion__Activa`), *Grabar cada ejecución* o *No grabar*.

**Por qué.** Medido el 22/10 con la imagen del extractor (1,5 CPU, Edge con una página que se redibuja), en % de un núcleo:

| Situación | CPU extra | Datos en ~25 s |
|---|---|---|
| Vista sin nadie mirando | ~0 % | 0 |
| Alguien mirando, como antes (Alta) | ~26 % (x11vnc 13 + Xvfb 11) | 16 MB |
| Refresco cada 200 ms (Media) | ~11 % | 7,5 MB |
| Refresco cada 500 ms + calidad 2 (Baja) | ~6 % | 3,5 MB |
| Grabación de vídeo (todo el caso, mire quien mire) | ~17 % | — |

La vista solo cuesta mientras alguien la tiene abierta (el panel del caso se conecta solo). Con el tope de 1,5 CPU eso bastaba para que Edge
no arrancase a tiempo en el servidor (ver `EXTRACTOR_CPUS`); con 4 CPU sobra margen. Si un robot va lento, primero el tope de CPU, luego
la vista (Baja o Apagada) y el vídeo.

**Cómo llega a cada sitio:**

- **Al que mira**, al momento: Viriato no enseña la pantalla de un despliegue con la vista apagada (ni en el caso ni la cámara del
  Listado), aunque el robot haya dado su dirección, y a la de noVNC le añade `quality`/`compression` del nivel. El marco pide `reconnect`,
  así que si el robot reinicia su servidor de pantalla vuelve solo.
- **Al robot**, por `GET /api/v1/rpa/despliegue`, que ahora trae `pantalla: { vista: { calidad, milisegundosEntreImagenes, calidadImagen,
  compresion } | null, grabarVideo: true | false | null }`. El robot lo lee cada pocos segundos mientras espera y cada 15 s mientras
  trabaja (`Worker__IntervaloAjustesDuranteUnPaso`): un cambio se aplica sin reiniciarlo y sin esperar a que acabe el caso. El vídeo
  sí se decide al empezar cada caso.

Aunque un robot aún no siga la plataforma, apagar la vista ya le quita el coste: si nadie se conecta, x11vnc no trabaja.

## Para quien quiera verlo

| Dónde | Qué |
|---|---|
| Página del caso | Panel **En directo** con la pantalla del robot (se puede ocultar o abrir en otra pestaña), y la barra de avance en el aviso «se está ejecutando». |
| Listado | La cámara junto a la situación del caso y una barra fina con el porcentaje. |
| Historial | Sin cambios; al terminar, el vídeo de la ejecución aparece como evidencia (filtro *Vídeos*). |
| Directamente | `http://localhost:6080/vnc.html` (solo desde el PC donde corre el contenedor). |

La cámara pide el mismo permiso que ver el caso (`casos.read`): la vista es de solo mirar.

## Verla desde fuera (cualquier usuario de Viriato, esté donde esté)

La vista se sirve por **la misma dirección de Viriato**, en `/vista/`: el Caddy de Viriato (`web`) le pasa al extractor las peticiones de
`/vista/*`. Así comparte el HTTPS, no hace falta un segundo nombre en el túnel y no hay problema de contenido mixto ni de marcos.

**La puerta es el login de Viriato.** La pantalla del robot enseña cómo se teclean el usuario y la contraseña del portal, así que no puede
quedar abierta a quien sepa la dirección:

```
 navegador ──POST /api/v1/casos/vista/pase──▶ API: ¿tiene casos.read? → cookie «viriato_vista» (HttpOnly, solo para /vista, 1 hora)
 navegador ──GET /vista/… (página, archivos, websocket)──▶ Caddy ──¿pase válido?──▶ API /api/v1/casos/vista/comprobar
                                                           ├─ 204: pasa al extractor (noVNC)
                                                           └─ 401: no pasa
```

El front pide el pase él solo cada vez que muestra una pantalla servida por la dirección de Viriato; quien mira no tiene que hacer nada
ni saber ninguna contraseña más. El pase va firmado con una clave derivada de `Jwt__SigningKey` (vale tras reiniciar y en varias copias de
la API) y no sirve como token de la API. Probado de punta a punta con Caddy: sin pase, 401 en la página y en el websocket; con pase, la
página llega y el websocket abre (101).

Para ponerlo en marcha, **en el PC donde corren Viriato y el extractor** (tienen que ser el mismo):

1. Actualiza Viriato: `git pull` y `docker compose -f docker-compose.prod.yml --profile tunnel up -d --build api web`. La red de Viriato
   pasa a llamarse siempre `viriato` (antes dependía del nombre del proyecto): Docker recrea los contenedores, los datos no se tocan.
2. Comprueba la puerta antes de enchufar el robot: `curl -I https://app.viriato.org/vista/vnc.html` tiene que dar **401**. (502 es un
   Viriato sin actualizar o sin extractor; 200 sin pase es que no está la puerta: no sigas.)
3. En el repositorio de los robots, `git pull` (su `docker-compose.vista-publica.yml` ya busca la red `viriato`) y en su `.env`:
   ```
   VISTA_EN_DIRECTO=true
   VISTA_URL_PUBLICA=https://app.viriato.org/vista/vnc.html?path=vista/websockify
   ```
   (`path=vista/websockify` le dice a noVNC por dónde abrir el websocket; sin él lo buscaría en la raíz.) `VNC_PASSWORD` ya no hace falta:
   si la pones, cada persona que mire tendrá que teclearla.
4. Arranca el extractor unido a la red de Viriato (el arranque automático, [arranque-automatico.md](arranque-automatico.md), ya lo hace así):
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.vista-publica.yml up -d extractor
   ```
5. Pruébalo desde el móvil **con datos**: entra en Viriato, abre un caso en ejecución y mira el panel *En directo*.

Límites: **un solo extractor** (con varias copias, el nombre `extractor` no sabría a cuál mandar) y los dos en el mismo PC. Si el extractor
corre en otra máquina alcanzable desde la de Viriato, pon su dirección en `VISTA_UPSTREAM` del `.env` de Viriato (por defecto
`extractor:6080`). Lo que se sirve en `/vista/` corre con la dirección de Viriato: pon detrás solo tus propios robots. Cloudflare Access sigue
siendo una capa extra opcional por delante de todo.

## Poner la dirección de la vista a mano (`VISTA_URL_PUBLICA`)

La dirección se abre **desde el navegador de quien mira**, así que tiene que ser una que ese navegador alcance:

- Viriato y robot en el **mismo PC**, entrando por `http://localhost:5173`: la de por defecto (`http://localhost:6080/vnc.html`). Esa no
  pasa por la puerta de Viriato: solo se ve desde ese PC.
- Viriato publicado por HTTPS: la de `/vista/` de arriba. Un navegador no deja incrustar una página `http` en una `https` (aunque `localhost`
  suele permitirse), y un segundo nombre en el túnel no tendría la puerta del login.
- El puerto 6080 solo se publica en `127.0.0.1` en el compose de los robots, a propósito.

## Para el desarrollador del robot

- `Viriato.Rpa.Client` 0.11.0 hace que el robot siga lo que se decide en Despliegues:
  - `builder.AddVistaEnDirecto()` (o `services.AddViriatoVistaEnDirecto(...)`) arranca y para **x11vnc + websockify** según la calidad del
    despliegue y los reinicia cuando cambia. El contenedor ya **no** debe arrancar x11vnc en su `entrypoint` (solo Xvfb). Lo que es de la
    máquina va en la sección `VistaEnDirecto`: `Permitida` (veto del robot), `Pantalla` (por defecto `DISPLAY`), `PuertoWeb` (6080),
    `PuertoVnc` (5900), `CarpetaNoVnc`, `Contrasena` (se le pasa a x11vnc en un fichero que borra al leerlo), `PermitirControl`. Sin
    `DISPLAY` (Windows) no hace nada. Frente a un Viriato anterior (sin `pantalla`) sirve la vista como antes.
  - `paso.VistaEnDirectoActiva`: si es `false`, no mandes `vistaUrl`.
  - `paso.GrabarVideo(segunElRobot: opciones.Activa)`: lo que decidió Viriato, o la opción del robot si lo dejó en sus manos.
  - Es el primero de una familia: `IAjusteDelDespliegue` recibe el estado del despliegue en cada lectura; otro ajuste que venga de la
    plataforma se registra igual.
- Para el extractor de BSH (repo `bsh_p01_prices_extractor`, necesita el token para reconstruir la imagen): subir a 0.11.0, añadir
  `builder.AddVistaEnDirecto()` en `Program.cs` mapeando `VNC_PASSWORD`/`VNC_CONTROL` a `VistaEnDirecto__Contrasena`/`__PermitirControl`,
  quitar x11vnc/websockify del `docker-entrypoint.sh`, y en `PreciosHandler` usar `paso.GrabarVideo(...)` y `paso.VistaEnDirectoActiva`.

- `Viriato.Rpa.Client` 0.7.0: `await paso.IntentarReportarEnVivoAsync(porcentaje: 40, mensaje: "…", vistaUrl: "…")` (no lanza nunca: cómo va un robot es
  para leerlo, no puede tumbar un paso) o `ReportarEnVivoAsync` si se quiere saber que falló. Hasta publicarla, el extractor usa `ReportadorEnVivo`, que
  hace la misma llamada con HTTP; se borra al subir de versión.
- El vídeo: `GrabadorDePantalla` (ffmpeg sobre la pantalla virtual). Con `Grabacion__Activa=false`, o sin `DISPLAY` (un PC con Windows), no hace nada.
  4 fotogramas por segundo, 1024 px de ancho, H.264 muy comprimido (unos 0,2 MB por minuto), fragmentado para que se vea aunque se corte, con tope de 25 MB
  (Viriato admite 30 MB por subida).
- Ejemplo de vídeo: `docs/ejemplo-grabacion-del-robot.mp4`.
