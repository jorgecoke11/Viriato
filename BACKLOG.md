# Backlog de trabajo autónomo

Lista de ideas para que Claude las vaya haciendo sin que haga falta hablar. Se escribe aquí, por orden de prioridad (la de
arriba va primero). Claude coge la primera de **Pendiente**, la termina, la verifica y la mueve a **Hecho** con una línea de lo que
hizo y lo que comprobó.

## Reglas

- **Terminar bien antes de pasar a la siguiente:** tests del backend y del front, `tsc` y eslint limpios, y verlo en el navegador
  cuando sea visible. Si algo falla y no se arregla, se anota en **Bloqueado** y se sigue con otra.
- **Si una idea es ambigua,** Claude elige la lectura más razonable, lo apunta bajo la tarea como «Decisión:» y sigue. No espera
  respuesta.
- **Cosas del front:** componentes genéricos en `components/ui`, pensando en lo que vendrá después (ver la memoria del proyecto).
- **Datos reales:** no se cancelan casos ni se tocan los robots. Las pruebas usan casos «ZZ prueba …» en «Proceso Alta».
- **Secretos:** no se pegan ni se usan tokens o contraseñas. Las claves van solo a archivos ignorados por git.
- **Commits:** `AUTORIZAR_COMMITS = no`. Cambia a `si` para que Claude commitee en una rama `trabajo/<tema>` al terminar cada tarea.
  Nunca hace push, abre PR ni toca `main` sin que se lo pidas.

## Pendiente

<!-- Añade aquí tus ideas, una por bloque:
### Título corto
Qué quieres, con el detalle que tengas. -->

_(Vacío: no queda nada pendiente. Añade aquí lo siguiente.)_

## Hecho

- Despacho por equipo con tiempo máximo por servicio (sin latidos), prioridades por ejecución, estados Pendiente / En ejecución,
  cancelar una ejecución y acciones masivas con registro extensible.
- Panel con cuatro grupos que no se solapan (en ejecución, pendientes, detenidos, finalizados) y filtros en el modal.
- Detalle del caso en pestañas a todo el ancho; historial visual; los archivos que generan los pasos van a Documentos.

- Etiquetas del caso distinguidas (tarea 1): `CasoEtiquetas` con rótulos «Situación / Estado de negocio / Tipo de caso» (cabecera) y tres
  columnas separadas en las columnas comunes de casos (`useColumnasDeCasos`); `DatoRotulado` genérico en `components/ui`. Verificado: tsc, eslint, 175 tests y navegador.

- Despachador por copias del robot (tarea 2): la capacidad sale de las copias en marcha (réplicas o procesos), una copia atascada solo
  ocupa su hueco y la prioridad se cumple bajo concurrencia; el tope por máquina pasa a opcional (vacío por defecto); el barrido de
  tiempos toma un cerrojo con varias réplicas de la API; el cliente manda su id de copia. Diseño en
  `docs/despacho-por-instancias.md`. Verificado: 149 tests de integración (7 nuevos de copias, con mutaciones), 81 del cliente,
  front (tsc, eslint, 175 vitest) y el diálogo de despacho en el stack real.
  - Decisión: el tope por máquina se **conserva como opcional** (hay robots que usan la pantalla y necesitan ir de uno en uno) y las
    máquinas que ya existían mantienen su valor. Para que sus copias trabajen en paralelo hay que vaciarlo en *Despacho*.

- Tiempo máximo agotado → estado de negocio final «Cancelado por exceso de tiempo de ejecución» (tarea 3): `MotivoDeCancelacion` en el
  orquestador, estado de sistema `CANCELADO_POR_TIEMPO` que se crea solo por proceso (como `DESCARTADO`); la cancelación manual sigue
  en «Descartado». Verificado con un test de integración (dos casos cortados en el mismo barrido comparten el estado; el historial lo
  recoge; el estado es final). Documentado en `docs/despacho-por-equipo.md`.

- Selector de rango de fechas genérico y visual (tarea 4): `Calendario` (rango con sombreado, teclado, lunes primero) y `SelectorDeRango`
  (atajos que siguen al día: hoy, ayer, 7 y 30 días, este mes, mes pasado; «todos» opcional; calendario; «Aplicar») en `components/ui`,
  lógica pura en `lib/rangoDeFechas.ts`; sustituye al modal «Finalizados de hoy» (panel y cada tarjeta); la elección se guarda por
  usuario (`usePreferencia`, clave con el id del usuario). Verificado: tsc, eslint, 200 vitest (25 nuevos) y en el navegador (atajos,
  rango, se conserva al volver, ajuste propio de una tarjeta). Decisión: se guarda en el navegador por usuario (no en el servidor).

- «Ver todos los casos»: elegir fechas antes de cargar (tarea 5): el botón de cada proceso abre `SelectorDeRango` (con «Todas las fechas»)
  y solo entonces va al listado con `?fechas=`; el listado tiene su propio selector de fechas de creación (por defecto, últimos 30 días,
  nunca el histórico entero) y recuerda la última elección por usuario; `rangoAParam/rangoDeParam` llevan la elección en la URL.
  Verificado: tsc, eslint, 203 vitest y en el navegador (la lista cambia con «Ayer» y «Todas las fechas»). Decisión: se filtra por la
  fecha de creación del caso.

- «Personalizar»: selector de procesos compacto (tarea 6): `MultiSelect` genérico en `components/ui` (un campo que dice cuántos hay elegidos,
  panel con buscador sin acentos, «marcar/quitar todos» —o los que coinciden— y lista con scroll; los elegidos, cuando son solo algunos,
  salen como chips que se pliegan tras 6 y se quitan de uno en uno); lógica pura en `lib/opciones.ts`. Se guarda por usuario y parte de
  lo que ya tenía guardado el navegador. Verificado: tsc, eslint, 210 vitest y navegador (quitar, buscar, marcar todos, guardado).

- Panel: estados de negocio finales distinguidos de los que están en curso (tarea 7): el desplegable de cada tipo lista los estados en dos
  bloques con cabecera y total («En curso»: círculo discontinuo azul; «Finales»: check verde y borde grueso), bajo el rótulo «Estado de
  negocio»: forma y color, no solo color. Verificado en el navegador con los datos reales de «Balay».

- Tablas genéricas (tarea 8): `useListaPaginada` (estado: búsqueda asentada, filtros, página, selección que sobrevive, «seleccionar todo lo
  que coincide», informe) + `ListaDeDatos` (dibujo) + `AccionesDeLista`, con lógica pura en `lib/lista.ts`. Acciones registrables: sin
  acciones no hay selección; añadir una es añadir una entrada. Migradas: Listado de casos (ahora con selección y acciones, buscador,
  situación, fechas y paginación), Acciones masivas, Trabajos y Mercados (paginación y filtros de texto reales) y todo `CrudPage`
  (paginación nueva; los wrappers de API ya reenvían `page`/`pageSize`, antes fijaban 100 e ignoraban `page`). Guía en
  `docs/tablas-genericas.md`. Verificado: tsc, eslint, 217 vitest y navegador (selección de las 277 que coinciden sin ejecutar nada,
  paginación, filtros). Lo que queda está en la 8b.

- Tablas genéricas, las que quedaban (tarea 8b): Credenciales, Despliegues y Plantillas de despacho (con la acción de cada fila como columna;
  Plantillas se busca y pagina en el cliente con `paginarEnCliente` porque el servidor la entrega entera) y la lista de casos del modal del
  panel pasan a `ListaDeDatos`. Se dejan como están las tablas pequeñas que son parte de un detalle (versiones de un flujo, detalle de
  compañía y de trabajo). Verificado: tsc, eslint, 219 vitest y navegador. Ver `docs/tablas-genericas.md`.

- Pestaña «Ejecuciones» rediseñada (tarea 9): cada ejecución es una tarjeta (la última arriba, «Actual» marcada, icono por estado, duración, pasos) que
  al abrirse muestra el proceso en una línea (`CasoProgreso`, «N de M resueltos») y los pasos como línea de tiempo (`Timeline` genérica, con
  nodo que gira si algo está en marcha): estado, tipo, intento, cuánto duró, **qué servicio y en qué máquina** lo ejecutó (campos nuevos
  `servicioNombre` / `equipoNombre` en el paso, con test de integración) y el error. Verificado: 151 tests de integración, tsc, eslint,
  219 vitest y navegador con un caso fallido real.

- Revisión final del trabajo sin commitear (tarea 10): sin archivos sueltos ni restos de depuración; borrado `CasosTabla` (sin usos tras migrar a
  `ListaDeDatos`) y `FinalizadosFiltroModal` (sustituido por `SelectorDeRango`); barrido de exportaciones sin uso del front; docs al día
  (`despacho-por-equipo`, `despacho-por-instancias`, `tablas-genericas`). Puertas finales: 7 proyectos de test del backend (151 de integración,
  39 Casos, 24 RpaFleet, 81 cliente, 12 Users, 100 Flujos, 29 Markets), solución completa compilada, front con tsc, eslint (0 errores),
  219 vitest y `vite build`.

- Creadores de caso (tarea 11): entidad nueva `CreadorDeCaso` del proceso (tipo, paso inicial por nombre, estado inicial, plantilla de título con
  `{creador} {fecha} {n} {datos.campo}`; contador por creador atómico), CRUD en la pestaña «Creadores» del proceso y creación sencilla
  (`GET /creadores-de-caso`, `POST /creadores-de-caso/{id}/casos`, permiso nuevo `casos.crear`) que pasa por las mismas comprobaciones que crear a
  mano. Front: `TarjetasDeOpciones` genérico, `NuevoCasoDesdeCreador`, página y modal nuevos (el formulario de siempre pasa a «avanzado»). El título
  se autogenera y el usuario puede cambiarlo. Verificado: 109 tests de Flujos (7 de la plantilla), 10 de integración nuevos (incluye 8 creaciones
  simultáneas con números distintos), tsc, eslint y navegador (crear creadores, elegir entre dos, crear un caso «ZZ prueba»). Ver `docs/creadores-de-caso.md`.
    - Decisión: el paso inicial se guarda por nombre (los pasos cambian de id en cada versión); el creador no guarda datos por defecto (se añade cuando haga falta).

- Parámetros del proceso editables desde el panel (tarea 12): el parámetro se **abre** a los usuarios (`editablePorUsuario` + nombre para el panel, en la
  pestaña Parámetros del proceso); el panel ofrece en la tarjeta del proceso una acción nueva «Cambiar parámetros» (solo si hay alguno abierto: el resumen trae
  `parametrosEditables`) con una ventana de un campo por parámetro que guarda todos o ninguno. Permiso nuevo `flujos.parametros` + estar asignado al proceso
  (si no, 404). Las acciones de la tarjeta pasan a una lista registrable (`ACCIONES_DE_PROCESO`: «Añadir caso» y «Cambiar parámetros»). Verificado:
  7 tests de integración nuevos (el robot lee lo que escribe el usuario, uno no abierto no se toca y no cambia nada, sin permiso 403, sin asignar 404,
  un cliente antiguo no cierra los campos nuevos), 8 de parámetros de siempre, tsc, eslint, 219 vitest y navegador (abrir un parámetro, cambiarlo desde la tarjeta).
  Ver `docs/parametros-del-proceso.md`.
    - Decisión: no hay historial de cambios de parámetros (solo `UpdatedAt`); se añade si hace falta saber quién cambió qué.

- Acciones masivas: pedir primero proceso y fechas (tarea 13): `ElegirAlcanceDeCasos` (tarjetas de proceso + `SelectorDeRango` + «Ver los casos») es lo
  primero que sale; hasta entonces no se carga nada. Lo elegido va en la URL (`?flujoId=&fechas=`, se puede compartir) y se recuerda por usuario; la
  lista (ya sin la columna Proceso ni el filtro «Todos los procesos») lleva el selector de fechas de creación y «Cambiar proceso». Verificado: tsc, eslint,
  navegador (primera pantalla, elegir «Proceso Alta», lista filtrada). Decisión: el proceso es obligatorio (no hay «todos») y las fechas empiezan en «Todas»
  porque un caso activo puede ser antiguo.

- Tipos de caso y estados finales diferenciados + lavado de cara del panel (tarea 14, con la skill ui-ux-pro-max: no depender solo del color y que los bordes
  de cada componente se vean por sí solos): cada **tipo de caso** tiene su color, que sale de su nombre (`lib/categorias.ts`, FNV-1a sobre seis tonos
  que no son de estado; los tipos reales de hoy no comparten color) y se ve igual en todas partes: ficha con su letra, borde y tinte en la tarjeta del panel
  (`FichaDeCategoria`) y etiqueta con letra en tablas y cabecera (`EtiquetaDeCategoria`); «Sin tipo» es gris. Los **estados de negocio** dicen ya si son
  finales (`EstadoNegocioDto.esFinal`): final = verde sólido con tick y negrita, en curso = contorno discontinuo azul, en el panel, el Listado y la cabecera
  del caso. Tokens nuevos teal / cyan / pink (claro y oscuro), el fondo claro algo más oscuro y la sombra de las tarjetas más firme para que lo blanco se
  separe. Verificado: 8 tests nuevos (colores estables, nunca de estado, sin choques entre los tipos de hoy), 227 vitest, tsc, eslint, `vite build` y navegador
  en tema claro (panel con los tipos de P01, estados en curso/finales, Listado).
    - Decisión: dos tipos pueden compartir color (hay seis tonos); por eso la letra y el nombre van siempre con él. El tema oscuro se comprobó en el Listado:
      los tonos nuevos se leen bien sobre fondo oscuro.

- Permisos de lo nuevo y rol de uso (tarea 15): permisos nuevos `casos.crear`, `casos.cancelar`, `casos.prioridad`, `casos.masivas`, `flujos.creadores`,
  `flujos.parametros`, `rpa.despacho` y `rpa.credenciales`, puestos en cada puerta del servidor y en cada botón/enlace del front. Admin los tiene todos (18, se
  completan solos al arrancar). El rol **User** (el que recibe quien se registra) empieza con los de usar la plataforma (ver panel y listado, crear, operar,
  cancelar, priorizar, acciones masivas, parámetros abiertos) y ninguno de configurar; los por defecto se dan solo si el rol está vacío y, luego, solo los
  permisos nuevos, sin devolver lo que un admin quitó. Crear un caso a mano (sin creador) y los tipos de documento pasan a pedir `flujos.manage`.
  Verificado: toda la suite del backend (183 de integración + 6 proyectos), 16 tests de integración nuevos (cada puerta, Admin con los 18, User con lo
  de usar y nada de gestionar), 5 unitarios de la regla de reparto, tsc, eslint, y la base real (Admin 18, User 8). Ver `docs/permisos.md`.
    - Decisión: no hay jerarquía entre permisos (cancelar no se deduce de `casos.manage`): cada uno se da expresamente. Los roles propios que ya existieran
      con `casos.manage` o `rpa.manage` deberán recibir los finos que necesiten. El nombre del rol sigue siendo «User» (el código lo usa por nombre).
    - Sin probar en el navegador con una cuenta de rol User (habría que crear una cuenta de prueba): la lógica de pantalla está cubierta por tsc y las puertas por tests.

- Esquema del despachador y despliegue en real (tarea 16): `docs/despachador-esquema.md` (piezas, la petición paso a paso, qué se da a quien pregunta, qué pasa
  cuando algo se cuelga, quién decide qué; diagramas Mermaid + ASCII) y `docs/despliegue-en-real.md` (tu PC + Docker + Cloudflare Tunnel, paso a paso, día a día,
  copias, seguridad y qué mirar si falla). Cableado en el repo: servicio `cloudflared` (perfil `tunnel`) en `docker-compose.prod.yml`, Caddy en HTTP plano con
  `DOMAIN=:80` detrás del túnel (validado el Caddyfile en las dos formas) y `PUBLIC_URL` para CORS. **Hallazgo arreglado:** en producción la API no aplicaba
  las migraciones (solo en Desarrollo); ahora lo hace con `Database:MigrateOnStartup` (el compose de producción lo activa). Verificado arrancando el compose de
  producción con una base vacía y valores desechables: 23 migraciones, Admin 18 permisos, User 8, administrador creado; luego se borró todo.
    - Decisión: la dirección fija exige un dominio con el DNS en Cloudflare (no toqué ningún DNS tuyo; las opciones están en la guía). El token del túnel solo va en `.env`.
    - Sin probar de punta a punta el túnel real (necesita tu cuenta y dominio de Cloudflare).

- Descargar documentos de forma masiva (tarea 18): acción nueva «Descargar documentos de N casos» en Acciones masivas (y en cualquier lista con casillas): un **zip con una
  carpeta por caso** y un `LEEME.txt`, con lo mismo que lista la pestaña Documentos (sin capturas ni vídeos). `POST /api/v1/casos/documentos/zip` (permiso nuevo
  `casos.descargar`, en el rol User): respeta la asignación al proceso, deja fuera y avisa de los casos inexistentes o sin documentos, limpia los nombres (nada sale
  de la carpeta; los repetidos se numeran), arma el zip en un temporal que se borra solo, tope de 500 casos / 5000 documentos / 2 GB por zip y varios zips numerados
  si la selección es mayor. El informe de la pantalla ahora admite cifras propias y omitidos «sin detalle». Verificado: 8 tests de integración (carpetas, repetidos,
  `..`, proceso ajeno, sin permiso, límites, 204 sin documentos), 9 unitarios de nombres, 17 de front (resumen, cabecera, tandas), tsc, eslint, `vite build` y el
  navegador con datos reales de P01 (zip de 583 bytes entregado y aviso «Se descargaron 1 documento de 1 caso»; Admin 19 permisos, User 9). Ver `docs/descarga-masiva-de-documentos.md`.
    - Decisión: la descarga no pide confirmación (no cambia nada); se baja un zip por cada 500 casos en vez de partir un único zip enorme.

- Dockerizar los RPAs (tarea 17), en el repo `bsh_p01_prices_extractor` (sin tocar nada de lo que ya tenías sin commitear): un `Dockerfile` por robot, `docker-compose.yml`
  (con `extractor` y `creador`), `.env.example`, `.dockerignore` (deja fuera `launchSettings.json` con tu clave, `.env` y `appsettings.Local.json`), `.gitattributes` (los `.sh`
  con LF) y una sección «Docker» en su README. El **creador** es solo .NET; el **extractor** lleva Microsoft Edge para Linux + Xvfb (una pantalla virtual: Edge se abre «con
  ventana» como en Windows) y corre como usuario sin privilegios. El token de GitHub Packages entra como secreto de BuildKit (no queda en la imagen). Verificado construyendo las dos
  imágenes (con un feed local con los mismos paquetes, para no usar ningún token): el creador valida su configuración y, con una clave falsa, llega a Viriato desde el contenedor y
  este la rechaza; en el extractor Edge 155 arranca en Xvfb como `robot` y **Selenium Manager baja el driver y gobierna Edge** (página abierta + captura de pantalla). La prueba
  encontró un fallo real —`selenium-manager` solo era ejecutable por root— y está corregido (`COPY --chown`). Escalar: `docker compose up -d --scale extractor=3`.
    - Decisión: Xvfb + `--no-sandbox` en un binario envoltorio en vez de `--headless`, para que el navegador se parezca lo más posible al de Windows; el driver lo baja Selenium Manager
      (volumen `selenium-cache`).
    - No probado con una clave real ni contra Tradeplace: serían casos reales (hay 73 pendientes de P01 en tu base). Ver «Bloqueado».

- Puertas finales de las tareas 11–18: backend con la solución completa compilada y 7 proyectos de test en verde (192 de integración, 53 Casos, 109 Flujos, 81 cliente, 29 Markets,
  24 RpaFleet, 17 Users), front con tsc limpio, eslint sin errores (3 avisos de siempre), 244 vitest y `vite build`.

- Proceso de trading, simple (tarea 19): proceso **Trading** creado por la API (sin tocar código) con estados (Pendiente de análisis → En análisis → Pendiente de aprobación →
  Operación registrada / Descartada, los dos últimos finales), tipo de caso «Operación» con formulario (valor, compra/venta, cantidad, precio límite, notas), versión publicada
  de tres pasos (Analizar operación → Aprobar operación [revisión humana] → Registrar operación) y el creador «Nueva operación» (título `compra SAN.MC 2026-10-09 #1`).
  Verificado: una operación de prueba (`ZZPRUEBA`) queda esperando aprobación y el formulario rechaza datos malos. Quedan en tu base el caso `compra ZZPRUEBA` y el proceso.
    - Decisión: sin robots por ahora (los pasos son internos y de revisión humana), a la espera de tus especificaciones.

- Acciones masivas y Listado, una sola pantalla (tarea 20): el Listado de casos es ahora también donde están las acciones sobre varios (cancelar, reprocesar, descargar documentos);
  `/casos/acciones-masivas` redirige al Listado y sale del menú. El proceso se elige con el **mismo `MultiSelect` que «Personalizar»** (todos marcados, se quitan los que sobran,
  buscador, chips; se recuerda por usuario) a través de un hook genérico, `useSeleccionPorExclusion`, con su lógica pura probada; el servidor acepta varios procesos
  (`flujoIds`, solo los asignados). «Ver todos los casos» del panel abre el Listado con solo ese proceso. Se borran `AccionesMasivasPage` y `ElegirAlcanceDeCasos`. Verificado:
  3 tests de integración nuevos (varios procesos, ajeno, basura), 244 vitest, tsc, eslint y navegador (todos → ninguno → solo Trading). Reemplaza la tarea 13 (que pedía el proceso antes).
    - Decisión: ninguno marcado = ninguna fila (no «todo»); las acciones se ven con `casos.masivas`.

- Menú de la izquierda y pantalla de inicio (tareas 21 y 22, con la skill ui-ux-pro-max): el menú tiene iconos en fichas, el elemento activo es una píldora rellena con degradado y
  brillo (se ve por la forma, no solo por el color), cabeceras de grupo con icono, tarjeta de usuario y un resplandor de marca arriba; el modo compacto usa el mismo relleno
  (`EnlaceDeMenu`/`EnlaceDeMenuCompacto`, que sustituyen a `navLinkClass`). La inicio ya no es un saludo: cabecera con saludo por hora y accesos, «Ahora mismo» (fallidos, esperan
  revisión, en ejecución, pendientes, cada uno lleva al Listado ya filtrado) y «Tus procesos». Verificado: tsc, eslint, vitest (saludo y fecha), navegador.
- Ver la ejecución en directo, vídeo, porcentaje y más estados en el timeline (tareas 23 y 24): ver `docs/ver-la-ejecucion-en-directo.md`. **Platform:** un canal único
  `POST /api/v1/rpa/pasos/{id}/en-vivo` (porcentaje, fase y dirección de la vista; solo http/https), campos nuevos en el detalle del paso (migración `EjecucionEnVivo`), el
  Listado dice qué casos están en vivo y su porcentaje.
  **Front:** panel «En directo» con la pantalla del robot dentro de la página del caso (solo de mirar), cámara en el Listado, barra de avance (`BarraDeProgreso`). El timeline NO lleva hitos (pedido del usuario).
  **SDK:** `ReportarEnVivoAsync`/`IntentarReportarEnVivoAsync` (0.7.0). **Robot (repo aparte):** noVNC + ffmpeg en la imagen, `GrabadorDePantalla` (vídeo del caso subido como
  evidencia, con o sin fallo) y `ReportadorEnVivo` (hasta que exista el SDK 0.7.0). Verificado: 5 tests de integración y 15 unitarios nuevos del servidor, 82 del SDK, 74 del robot
  (14 nuevos), 267 vitest, tsc, eslint; noVNC mostrando el Edge del contenedor en el navegador y un vídeo de ejemplo (`docs/ejemplo-grabacion-del-robot.mp4`).
    - Decisión: no hay Selenium Hub (reparte sesiones; aquí solo se quiere mirar) sino noVNC dentro de cada contenedor; el 6080 solo se publica en 127.0.0.1.
    - Falta ver el icono y la barra con un caso real en marcha (requiere cambiar el extractor a la imagen nueva entre dos casos).

## Bloqueado / necesita al usuario

- **Abrir el PR:** hay que commitear todo lo hecho desde el último commit. Lo hace Claude cuando se lo pidas.
- **Publicar los paquetes** `Viriato.Rpa.Client` 0.7.0 y `Viariato.ApiContracts` 1.4.0 con tu token
  (`backend/sdk/publish-packages.ps1`). Hasta entonces los robots con 0.6.0 no pueden enviar `Prioridad` ni su id de copia (sin él,
  todas las réplicas de un robot cuentan como una sola copia).
- **Tu «Maquina Test» tiene el tope en 2:** para que las réplicas de un robot trabajen en paralelo, vacía el campo en *Despacho*.
  No lo he tocado porque tus robots reales corren contra ese stack.
- **Servicios sin tiempo máximo:** si un robot muere sin informar, su paso queda ocupado hasta que se configure un tiempo máximo en
  su servicio. Decide si quieres uno por defecto.
- **Probar los robots en contenedor con una clave real:** necesita el `GITHUB_PACKAGES_TOKEN` (para construir) y la clave de cada despliegue. Arrancar el extractor contra tu
  Viriato empezaría a tomar los casos pendientes de verdad. Hazlo cuando quieras, p. ej. con un caso de prueba.
- **Varias copias de un robot** cuentan como una sola hasta publicar `Viriato.Rpa.Client` 0.7.0 y subir su versión en los dos `.csproj` de los robots.
- **Cloudflare Tunnel de verdad:** necesita tu cuenta y un dominio con el DNS en Cloudflare (ver `docs/despliegue-en-real.md`); no he tocado ningún DNS tuyo.
- **Revisa la prueba de «ZZ prueba»:** quedaron en tu base de desarrollo el caso «ZZ prueba creador urgente 2026-10-08 #1» (+ el otro de la primera prueba), dos creadores
  «ZZ prueba creador…» y el parámetro `zz_prueba` de «Proceso Alta». Bórralos cuando quieras (o dime y lo hago).
- **Dos casos reales fallaron por el propio robot** (no por la plataforma): «Productos de Limpieza Bosch» (una celda de precio vacía hace reventar `Double.Parse` en
  `CalculadorPrecios`) y «Lavavajillas Balay» (motivo sin mirar). Decide qué hacer con una celda sin precio (saltarla, dejarla vacía o fallar con un mensaje claro).
- **Publicar la vista en directo fuera de tu PC** (Cloudflare Tunnel) necesita un segundo *Public hostname* para el puerto 6080 y protegerlo con Cloudflare Access: ver
  `docs/ver-la-ejecucion-en-directo.md`.
