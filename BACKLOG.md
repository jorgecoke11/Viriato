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

> Todo lo de interfaz de esta lista se hace con **componentes genéricos** de `components/ui` (regla del proyecto) y con la skill `ui-ux-pro-max`:
> se cambia el componente, no cada pantalla.

### 26. Menos texto, más iconos — a medias
En muchas pantallas hay demasiado texto. Repasar el panel y los componentes genéricos y sustituir texto por icono (con `title`/etiqueta accesible)
donde el icono se entienda solo; dejar texto solo donde aporte. Empezar por: tarjetas de proceso, filas del Listado, cabeceras de las tablas CRUD y el
detalle del caso. Criterio: una pantalla se entiende de un vistazo, sin leer párrafos.
**Hecho hasta ahora (parte de la tarea):** los contadores de situación, el «Activo» de las tablas y las acciones de los CRUD ya se leen por su icono (tareas 28 y 30), y se quitó el
rótulo «Estado de negocio» del tipo de caso (27). **Falta y necesita tu criterio:** qué pantallas te parecen más cargadas de texto; no he recortado explicaciones a ciegas porque
algunas orientan al usuario nuevo (ver «Dudas»).


### 31. Trading con IA local — fases siguientes
Planteamiento en [docs/trading-ia-local.md](docs/trading-ia-local.md); código en `C:\Users\Jorge\Desktop\Proyectos jorge\trading_robots`. **Hecho:** fase 0 (datos, indicadores, reglas, riesgo, cartera simulada), la prueba simple de la fase 1
(IA local como segunda opinión), la cartera en Viriato, el **funcionamiento 24 h** (planificador por horas del mercado, sin órdenes repetidas) y el **aprendizaje** (juzga análisis, propone pesos, reentrena un modelo con puerta de validación).
**Siguiente:** que el modelo aprendido pueda decidir (hoy solo se mide), ver en pantalla la lectura de la IA junto a las reglas, y decidir en qué PC corre (ver «Dudas»). La estructura actual es **un solo proceso («Trading simulado»)**, ver «Hecho».

### 34. Selector desplegable de creador de caso, con buscador — pendiente (apuntado, sin empezar)
Componente genérico para el modal «Nuevo caso» del panel, en lugar de las tarjetas de opciones: un **desplegable de selección única** con los creadores de caso del proceso y un **buscador** dentro de la lista,
para que el modal sea más pequeño. Pedido por el usuario el 10/10 («lo apuntas, no lo hagas ahora»). Notas para cuando se haga: ya existen `Popover` y `SearchField` (los usa `MultiSelect`, que es de selección múltiple) y
`opcionesDeCreadores` (nombre, descripción, proceso y tipo de cada creador); `TarjetasDeOpciones` se queda para lo que necesite verse en tarjetas. Nombre sugerido: `SelectorConBusqueda` en `components/ui`, genérico (no sabe qué son las opciones),
con teclado (flechas, Enter, Escape), `aria-activedescendant` y «Nada coincide con…». El modal debería pasar a tamaño `md` y, si el proceso tiene un solo creador, seguir sin pedir elección.

### 35. Rehacer la vista de procesos del panel (tipos de caso) — pendiente, con preguntas
Pedido el 10/10 con `/design`: cambiar el enfoque de todo el componente de proceso del panel (tarjeta de proceso, tipos de caso, filas, estados), menos «de IA» y más enfocado al usuario. **Antes de empezar, preguntar qué es lo que no encaja**: los dos rediseños
anteriores del panel no gustaron y se deshicieron (ver «Rediseños del panel — probados y DESHECHOS»). Ya está hecho el cambio de estilo global (claro, cálido, azul petróleo), que el panel hereda.

### 36. Opciones de un desplegable que salen de un parámetro del proceso — pendiente (apuntado, sin empezar)
**Ojo (22/10):** los esquemas ya no están en el tipo de caso sino en los **formularios** del proceso ([docs/formularios.md](docs/formularios.md),
apartado «Ampliar»): la comprobación «al guardar el esquema» es al guardar el **formulario**, y los valores por defecto de los creadores también pasan por ella.
Pedido el 16/10. Hoy las opciones de un `enum` se escriben fijas en el esquema del **tipo de caso** (p. ej. `"proveedor": {"enum": ["Balay"]}`); se quiere que puedan salir de
un **parámetro del proceso** que guarde la lista (`proveedores` = `["Balay", "Bosch", "Siemens"]`), para que añadir un proveedor no obligue a tocar el esquema. Si ese parámetro
está abierto al panel, quien trabaja el proceso lo amplía desde *Cambiar parámetros*. Notas para cuando se haga:
- **Extensión del esquema**, p. ej. `"x-opciones-del-parametro": "proveedores"` en un campo `string`/`number`, con `enumNames` opcional (o una lista `[{valor, nombre}]` en el parámetro).
  Pensarlo como fuente genérica de opciones (hoy parámetros; mañana quizá otra cosa), no como caso suelto.
- **Dos validadores a la par**: `frontend/src/components/schema-form/esquema.ts` y `backend/src/Viariato.Modules.Flujos/Esquemas/EsquemaDatos.cs` aceptan y rechazan lo mismo.
- **Al guardar el esquema**: el parámetro existe en el proceso y su valor es una lista válida; si no, error claro.
- **Al dibujar el formulario**: ruta que dé **solo las listas que usa el esquema** a quien tiene `casos.crear` y está asignado al proceso (leer parámetros hoy es `flujos.manage`/`flujos.parametros`).
- **Al crear el caso**: el servidor comprueba el valor contra la lista **actual** del parámetro. Si el parámetro desapareció o dejó de ser una lista, se rechaza con un mensaje que lo diga.
- **Casos antiguos** conservan su valor aunque salga de la lista; solo deja de ofrecerse.
- Tests de los dos validadores, de la ruta (permisos y asignación) y de la creación con un valor de fuera de la lista; verlo en el formulario de «Nuevo caso».

### 37. Vigilante de precios: leer Amazon con navegador — pendiente (apuntado, sin empezar)
Pedido el 22/10 con el robot `vigilante_precios`. Amazon contesta a quien no es un navegador con una comprobación anti-robots (HTTP 202 y una página que se resuelve
con JavaScript), así que sus casos acaban en ERROR («ha pedido una comprobación anti-robots») y su lanzador viene en pausa. Para leerlo: un lector de Amazon con
navegador (Selenium/Edge como el robot de BSH, con su imagen pesada) detrás de la misma interfaz `ILectorDePrecio`, solo para esa tienda. Ojo con lo aprendido con el
extractor de BSH en el servidor: CPU suficiente (`EXTRACTOR_CPUS`) y `/tmp` en memoria.

### 38. Robot de BSH: no fallar el caso cuando no se puede abrir Edge, y aligerar la cámara — pendiente (apuntado, sin empezar)
Del 16/10. (1) Si Edge no arranca, el fallo es de la máquina, no del caso: el robot debería devolver el caso a la cola (o no cogerlo) y reiniciarse, en vez de quemar la cola.
(2) La cámara: la calidad y el vídeo ya los decide la plataforma (ver «Hecho», 22/10); falta pasar el extractor a `Viriato.Rpa.Client` 0.11 (`AddVistaEnDirecto`, sin
x11vnc en el entrypoint, `paso.GrabarVideo`). Quedan como ideas: pantalla 1366×768 y que la página del caso no la conecte sola. Va en el repo `bsh_p01_prices_extractor`
y necesita reconstruir su imagen (token).

## Hecho

- Vista en directo y vídeo decididos por la plataforma (22/10): en Despliegues → «Pantalla del robot», vista Apagada/Baja/Media/Alta (los existentes, Baja) y vídeo
  «lo decide el robot»/grabar/no grabar. Viriato deja de enseñar la vista apagada y pone la calidad de noVNC en la dirección; el robot lo lee de `GET /rpa/despliegue`
  (`pantalla`). SDK 0.11.0: `IAjusteDelDespliegue` (primero de una familia, releído cada 15 s durante un paso), `AddVistaEnDirecto` arranca/para x11vnc + websockify
  con el refresco del nivel, `paso.GrabarVideo(...)`. Medido en la imagen del extractor: mirar en Alta ~26 % de un núcleo, en Baja ~7 %, sin nadie ~0, el vídeo
  ~17 % siempre. Doc en `docs/ver-la-ejecucion-en-directo.md`. Verificado: backend completo (una intermitente de despacho que pasa sola), front (tsc, eslint, vitest)
  y en el navegador sobre una copia con un robot de prueba del SDK en la imagen del extractor (Baja → Alta a mitad de caso → Apagada). Falta: pasar el extractor a 0.11 (tarea 38).

- Reprocesamientos (22/10): pestaña propia en cada proceso. Una regla dice cuándo (estado de negocio y/o cómo acabó la última ejecución), qué
  paso se relanza (o el que terminó), cuánto esperar, el límite opcional por caso y el estado final al agotarlo; gana la primera por orden.
  Se decide al acabar el caso, en el mismo guardado (interceptor como las notificaciones), y lo ejecuta un reprocesador cada 10 s; los
  descartes manuales nunca se reprocesan. Historia del caso con filtro «Reprocesos». Arreglado de paso: tras reprocesar un paso anterior, los
  siguientes se crean como intento nuevo (antes chocaban con el índice único). Doc en `docs/reprocesamientos.md`. Verificado: backend
  completo, front (tsc, eslint, vitest) y en el navegador sobre una copia de la base (3 reprocesos y el caso a «Error» al cuarto fallo).

- Formularios fuera de los tipos de caso, asociados a los creadores (22/10): biblioteca de formularios por proceso (compartibles), valores por defecto en el creador
  (modo parcial) y en el lanzador (solo lo que cambia, mezclado campo a campo), el caso recuerda su formulario, robots con `Creador` opcional, migración sin pérdida.
  Componentes genéricos `DatosConFormulario`, `ListaDeDatosConFormulario`, `EditorDeEsquema`, `ControlSegmentado` y campos `custom` en el kit CRUD. Diseño en
  `docs/formularios.md`. Verificado: backend completo (254 de integración + unitarias), front (tsc, eslint, 293 vitest) y en el navegador sobre una copia de la base.

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

- **Tipografía (tarea 25):** la letra pasa a la del sistema de Apple donde existe (San Francisco en iPhone y Mac) y a **Inter**, incluida en la web, en Windows y Android
  (`index.css`: `--font-sans`; `@fontsource/inter` sustituye a Fira Sans; Fira Code se queda para el código).
- **Revisiones desde la web (tarea 32):** un caso que espera revisión muestra un aviso con **Aprobar** y **Rechazar** (comentario opcional, cada decisión pide confirmación) en su
  página; solo con el permiso `casos.review`. Componente `RevisarPaso`; llama a `POST /api/v1/revisiones/{ejecucionPasoId}/resolver`. Verificado en pantalla.
- **Selector de fecha (tarea 29):** un clic en el calendario es **un día entero (24 h)**; un segundo clic lo amplía a rango; con `conHora` (el Listado lo usa) se puede acotar
  desde/hasta qué hora. Cambios en `lib/rangoDeFechas.ts` (`Hora`, `instanteDesde/Hasta`, enlace con `T09:00`), `SelectorDeRango` y el Listado. 5 tests nuevos.
- **Contadores e iconos (tareas 28 y 30) — probado y DESHECHO por no gustar:** hice un componente de contadores con icono grande en bloque, «Activo» con icono de encendido y acciones de
  fila con fondo de color; se vieron demasiado llamativos y el usuario pidió volver a los iconos sutiles de antes. Quedan como estaban (chips de las tarjetas de proceso, `ActivoBadge`,
  `IconButton`, iconos de 16 px). **Se conserva** del trabajo de la 30: `Badge` como rectángulo redondeado (ya no píldora) y el tamaño `lg` de la situación, el estado de negocio y el tipo
  en la página del caso. Si se retoma, hay que preguntar primero qué es lo que molestaba exactamente (el tamaño, el color de fondo o la forma).
- **Trading, fase 1 simple y cartera en Viriato (tarea 31):** módulo nuevo `Viariato.Modules.Cartera` (tablas `cartera.estado/movimientos/cotizaciones`, migración `Cartera`, permiso `cartera.read`
  para Admin y el rol User), API de los robots (`/api/v1/rpa/cartera`: leer, registrar un movimiento idempotente por caso, guardar cotización) y de las personas (`GET /api/v1/cartera`), y la pantalla **Cartera**
  (menú, patrimonio, efectivo, invertido, rentabilidad, posiciones y movimientos). Los robots usan esa cartera por defecto. 3 pruebas de integración + 6 de formato. La IA local (Ollama, `qwen2.5:3b`) da una
  lectura como evidencia del caso con el parámetro `ia_activa`; **probado con datos reales**: carga ~3 min la primera vez y ~1 min por valor en CPU; devuelve JSON válido pero repite lo que dicen las reglas.
  El Dockerfile de la API ahora copia el proyecto de Cartera (sin eso la imagen no construía). Probado de punta a punta en pre: análisis de SPY con IA → orden → aprobada → ejecutada → visible en Cartera.
- **Lanzadores de caso — el cron de la plataforma (tarea 33, 10/10):** entidad nueva del proceso, al lado de los creadores: un creador que se dispara solo con una o varias expresiones **cron** (5 campos, una por línea), zona horaria, datos JSON del caso y tolerancia.
  Backend: `Flujos` (entidad `LanzadorDeCaso`, tabla `flujos.lanzadores_de_caso`, migración `Lanzadores`, API `/api/v1/flujos/{id}/lanzadores`, lógica pura `CronDeLanzador` con la librería Cronos) y `Casos` (`AbridorDeCasos`, que abre el caso sin usuario ni HTTP con las mismas comprobaciones que un creador, y `VigilanteDeLanzadores`, que mira cada 30 s con cerrojo para varias réplicas).
  Un reinicio no repite un momento, los momentos perdidos se saltan pasada la tolerancia (varios perdidos → un caso), un fallo se ve en el propio lanzador. Front: pestaña **Lanzadores** en el proceso. Pruebas: 15 de integración (cron en zona, tolerancia, no repetir, pausa, error) y 10 unitarias (incl. cambio de hora). Documentado en [docs/lanzadores-de-caso.md](docs/lanzadores-de-caso.md).
- **Trading en UN proceso, con los servicios uno detrás de otro (tarea 31, 10/10):** a petición del usuario («la premisa de esta aplicación son las transiciones de servicios; los casos tienen una ejecución con un servicio asociado; un solo proceso, no tres»).
  Antes eran T01, T02 y «Trading» y los robots se pasaban el trabajo **creando casos**. Ahora: proceso **«Trading simulado»** con tres pasos —Analizar valor (Analista) → Ejecutar orden (Ejecutor) → Aprender (Aprendiz)— y dos tipos de caso: **Valor** (empieza en el primer paso; si no hay orden termina ahí; si la hay, el caso pasa solo al Ejecutor)
  y **Aprendizaje** (empieza en el tercero). **Sin aprobación humana** (lo pidió el usuario): el Ejecutor ejecuta lo que propone el Analista, dentro de los límites de riesgo y siempre en la cartera simulada. Desaparecen el robot Lanzador y el caso «Lote».
  Dos mejoras genéricas de la plataforma para hacerlo posible: (1) **un paso puede escribir en los datos de su caso** (`POST /api/v1/rpa/pasos/{id}/datos`, SDK 0.8.0 `ActualizarDatosCasoAsync`, contratos 1.4.0): así el Analista deja la orden para el Ejecutor; (2) **un lanzador con lista**: sus datos pueden ser una lista de objetos y abre **un caso por elemento**
  (la lista de valores vive ahora en el lanzador, no en un parámetro). Lo viejo (T01, T02, «Trading», con sus casos y claves) quedó **desactivado, no borrado**, y sus lanzadores en pausa. Pasar un caso a otro **proceso** (regla «cuando acabe en tal estado, crea un caso en otro proceso») queda como excepción y **no está construido**.
  Probado en pre: el lanzador abrió un caso por valor y los robots los analizaron de punta a punta; 232 pruebas de integración, 95 en los robots, 83 del cliente. **Pendiente del usuario:** publicar `Viriato.Rpa.Client` 0.8.0 y `Viariato.ApiContracts` 1.4.0 en el feed de GitHub (`backend/sdk/publish-packages.ps1`, necesita su token) para poder construir las imágenes de los robots; en este PC se construyen con la carpeta `paquetes-locales/`.
  Aprendizaje: el aprendiz juzga análisis tras 5 días, propone pesos con ≥30 muestras por regla (una persona los aplica en Cartera) y reentrena una regresión logística con validación temporal y puerta campeón/aspirante; el primer modelo con datos reales quedó «Rechazado» (56,5 % frente a 56,6 % de la apuesta obvia). Cripto 24/7 y velas por horas: **aparcado**, lo define el usuario.
- **Rediseños del panel — probados y DESHECHOS por no gustar (10/10):** unir «en curso» con los pendientes en dos grupos, el icono de flechas para «en curso» con los finalizados en gris,
  tres cifras de negocio en banda, y reconstruir el panel entero como lista de procesos (filas con reparto, cifras y estados desplegables). Se volvió al panel de antes. **Lo único que
  se conserva es quitar el rótulo «Estado de negocio»** de dentro de cada tipo de caso. Antes de volver a tocarlo, preguntar qué es lo que no encaja.
- **Rótulo «Estado de negocio» (tarea 27):** quitado de dentro de cada tipo de caso en las tarjetas de proceso (interpretado así: era el único sitio donde aparecía dentro del tipo).

## Bloqueado / necesita al usuario

- **Formularios de creador (22/10):** al desplegar, la migración `FormulariosDeCreador` pasa los 14 esquemas de los tipos a formularios (probada sobre una copia de la
  base de desarrollo: 14 formularios, 11 creadores y 320 casos enlazados). Los robots que crean casos **por tipo** dejan de tener sus datos comprobados contra ese
  esquema hasta que nombren un creador (`Creador`, `Viriato.Rpa.Client` 0.10 / `Viariato.ApiContracts` 1.5, **sin publicar**: necesita tu token).

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

## Dudas (para plantear al usuario)

*Respondidas el 10/10: valores de la lista (vale AAPL, MSFT, NVDA, SPY y QQQ), IA local con Ollama (hecha la prueba simple) y cartera en Viriato (hecha). Pendiente de explicar lo de las aprobaciones.*

1. **Menos texto, más iconos (26):** ¿qué pantallas te parecen más cargadas de texto? No recorté explicaciones a ciegas.
2. **Aprobaciones de órdenes:** el usuario no entendía de dónde salen. Vienen del proceso Trading de pre (paso «Aprobar operación», revisión humana) que se conservó en T02 antes de ejecutar. Decidir si se
   mantiene (una persona aprueba cada orden simulada) o si el ejecutor opera solo mientras es de papel.
3. **Dónde correrá el trading:** en qué PC (RAM / GPU NVIDIA) y con qué modelo; hoy la IA tarda ~1 min por valor en este PC.
4. **Horas de los lanzadores de trading (hechas con valores por defecto):** análisis 10:00, 12:30 y 15:45 (hora de Nueva York) y aprendizaje 16:30, solo laborables. ¿Te valen? Se cambian en el proceso T01 → Lanzadores. **Ojo:** ya están activos en pre, así que el lunes abrirán casos de análisis aunque no haya robots escuchando (se quedarán en cola).
5. **Festivos de EE. UU.:** el cron abre casos también en festivos (el mercado está cerrado y Yahoo devuelve el último día, así que no se opera con datos nuevos, pero se gasta un análisis). ¿Añado un calendario de festivos?
6. **¿Debe decidir el modelo?** Hoy solo se mide. Propongo no dejarlo decidir hasta tener meses de acierto «en la práctica» por encima de la apuesta obvia.
