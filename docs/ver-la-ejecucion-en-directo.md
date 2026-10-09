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

## Para quien quiera verlo

| Dónde | Qué |
|---|---|
| Página del caso | Panel **En directo** con la pantalla del robot (se puede ocultar o abrir en otra pestaña), y la barra de avance en el aviso «se está ejecutando». |
| Listado | La cámara junto a la situación del caso y una barra fina con el porcentaje. |
| Historial | Sin cambios; al terminar, el vídeo de la ejecución aparece como evidencia (filtro *Vídeos*). |
| Directamente | `http://localhost:6080/vnc.html` (solo desde el PC donde corre el contenedor). |

La cámara pide el mismo permiso que ver el caso (`casos.read`): la vista es de solo mirar.

## Verla también desde fuera (la forma recomendada)

La vista se sirve por **la misma dirección de Viriato**, en `/vista/`: el Caddy de Viriato (`web`) le pasa al extractor las peticiones de
`/vista/*`. Así comparte el HTTPS y el login de Cloudflare Access, no hace falta un segundo nombre en el túnel y no hay problema de
contenido mixto ni de marcos. Probado: la página de noVNC, sus archivos y el websocket llegan por `/vista/` (el websocket responde 101).

1. Viriato (con el `Caddyfile` nuevo: `docker compose … up -d --build web`) y el extractor tienen que estar **en el mismo PC**, y el extractor
   en la red de Docker de Viriato. En el repositorio de los robots, `docker-compose.vista-publica.yml` lo hace:
   ```powershell
   docker compose -f docker-compose.yml -f docker-compose.vista-publica.yml up -d extractor
   ```
2. En el `.env` de los robots:
   ```
   VISTA_EN_DIRECTO=true
   VISTA_URL_PUBLICA=https://app.viriato.org/vista/vnc.html?path=vista/websockify
   VNC_PASSWORD=<una contraseña>
   ```
   (`path=vista/websockify` le dice a noVNC por dónde abrir el websocket; sin él lo buscaría en la raíz.)
3. Quien mire entra con su login de Access, abre el caso en ejecución y ve el panel. Con `VNC_PASSWORD`, noVNC pide además esa contraseña.

Límites: **un solo extractor** (con varias copias, el nombre `extractor` no sabría a cuál mandar) y los dos en el mismo PC. Si el extractor
corre en otra máquina, cambia el destino con la variable `VISTA_UPSTREAM` del servicio `web` (por defecto `extractor:6080`).

## Poner la dirección de la vista a mano (`VISTA_URL_PUBLICA`)

La dirección se abre **desde el navegador de quien mira**, así que tiene que ser una que ese navegador alcance:

- Viriato y robot en el **mismo PC**, entrando por `http://localhost:5173`: la de por defecto (`http://localhost:6080/vnc.html`).
- Viriato publicado por HTTPS: la de `/vista/` de arriba. Un navegador no deja incrustar una página `http` en una `https` (aunque `localhost`
  suele permitirse), y un segundo nombre en el túnel necesitaría su propio Access.
- El puerto 6080 solo se publica en `127.0.0.1` en el compose de los robots, a propósito.

## Para el desarrollador del robot

- `Viriato.Rpa.Client` 0.7.0: `await paso.IntentarReportarEnVivoAsync(porcentaje: 40, mensaje: "…", vistaUrl: "…")` (no lanza nunca: cómo va un robot es
  para leerlo, no puede tumbar un paso) o `ReportarEnVivoAsync` si se quiere saber que falló. Hasta publicarla, el extractor usa `ReportadorEnVivo`, que
  hace la misma llamada con HTTP; se borra al subir de versión.
- El vídeo: `GrabadorDePantalla` (ffmpeg sobre la pantalla virtual). Con `Grabacion__Activa=false`, o sin `DISPLAY` (un PC con Windows), no hace nada.
  4 fotogramas por segundo, 1024 px de ancho, H.264 muy comprimido (unos 0,2 MB por minuto), fragmentado para que se vea aunque se corte, con tope de 25 MB
  (Viriato admite 30 MB por subida).
- Ejemplo de vídeo: `docs/ejemplo-grabacion-del-robot.mp4`.
