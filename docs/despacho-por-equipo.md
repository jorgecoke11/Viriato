# Despacho por equipo: quién va primero

Varios robots (despliegues) pueden vivir en la misma máquina. Antes, cada uno cogía lo más antiguo de **su** cola sin
mirar a los demás, así que dos robots del mismo equipo podían trabajar a la vez, sin orden. Ahora el equipo decide.

> **Esquema:** el mapa de todo el despachador, con diagramas, está en [despachador-esquema.md](despachador-esquema.md).
>
> **Capacidad y réplicas:** cuántas ejecuciones corren a la vez lo deciden las **copias del robot** que haya en marcha (réplicas
> del stack o procesos en Windows), no un número del front: ver [despacho-por-instancias.md](despacho-por-instancias.md). El
> «límite de ejecuciones a la vez» de la máquina es **opcional**; lo que sigue sobre el orden y los turnos aplica cuando se pone.

## Qué se configura

Por cada **equipo** (RPA → Equipos → botón *Despacho*):

| Ajuste | Qué hace |
|---|---|
| **Límite de ejecuciones a la vez** (opcional) | Un tope de pasos que puede estar ejecutando a la vez la máquina. **Vacío por defecto**: sin límite, mandan las copias de los robots. Con **1**, los robots van uno detrás de otro (lo seguro si usan la pantalla o el mismo navegador). |
| **Cómo elegir** | *Por prioridad*: va primero el servicio más arriba de la lista que tenga trabajo, siempre. *Por turnos*: se turnan; tras un servicio va el siguiente de la lista que tenga trabajo, y al llegar al final se vuelve a empezar (ninguno se queda sin turno). |
| **Orden de los servicios** | La lista. Un servicio que la máquina ejecuta pero no está en la lista va **después** de todos los de la lista. Sin lista, se atiende por orden de llegada. |

Las **plantillas** (RPA → Plantillas de despacho) guardan esos tres ajustes para reutilizarlos. Aplicar una plantilla a un
equipo **copia** sus valores: el equipo queda con su propio orden, que puede cambiar sin tocar la plantilla ni a los demás
equipos, y borrar la plantilla no cambia ningún equipo. En el botón *Despacho* de un equipo también se puede guardar su
configuración actual como plantilla nueva.

El mismo diálogo muestra, a la derecha y actualizado cada pocos segundos, lo que la máquina está haciendo y lo que espera,
en el orden en que se atenderá, con el motivo cuando algo no avanza (robot apagado, sin conexión u ocupado, servicio al
límite), y a cada paso en ejecución cuánto le queda antes de que su servicio lo cancele (o *Sin tiempo máximo*).

## Cómo se decide

Cuando un robot pide trabajo (`POST /api/v1/rpa/cola/siguiente`) la API no le da directamente lo suyo:

1. Si esa copia ya tiene un paso en ejecución, la respuesta es «nada ahora» (204, igual que una cola vacía).
2. **Sin límite en la máquina** (lo normal): recibe el primero de la cola de su robot (mayor prioridad y, a igualdad, el que lleva
   más esperando). Cada robot trabaja su cola; varias copias del mismo robot se reparten la cola en orden.
3. **Con límite**: si la máquina ya ejecuta tantos pasos como el límite, «nada ahora». Si no, mira qué tiene esperando **cada
   robot con alguna copia libre** y elige el primero según la regla: mejor posición en el orden (o en la rotación) y, a igualdad,
   lo que lleva más tiempo esperando. Solo ese robot recibe el paso; los demás vuelven a preguntar a los pocos segundos.

## Pendiente, en ejecución y cancelar

Un caso nace **Pendiente**: su primer paso espera en la cola a que un robot lo coja. Pasa a **En ejecución** en cuanto un robot lo
reclama, y vuelve a **Pendiente** cada vez que pasa a otro paso que tiene que coger un robot (o se reprocesa uno). Los pasos que
no son de robot (API, agente…) se ejecutan al momento. Un caso pausado sigue **Pausado** aunque un robot coja su paso.

- El panel reparte los casos en **cuatro grupos que no se solapan**, según lo que está haciendo el caso (su estado técnico):
  **En ejecución** (`EnProgreso`), **Pendientes** (`Pendiente`), **Detenidos** (`Iniciado`, `Pausado`, `EsperandoRevisionHumana`;
  solo se ve si hay alguno) y **Finalizados** (`Completado`, `Fallido`, `Cancelado`). Los cuatro suman el total. Cada uno enlaza al
  listado ya filtrado, y el parámetro `estado` del listado admite varios separados por comas. El *estado de negocio* del flujo
  («Alta recibida»…) es otra pregunta: se filtra aparte y no define estos grupos. Los grupos se definen en un solo sitio,
  `frontend/src/features/casos/situaciones.ts`.
- **Cancelar una ejecución** (un paso RPA que espera o que un robot ejecuta) cancela también su caso: los pasos van uno detrás
  de otro. Está en la página del caso, en la pestaña *Ejecuciones* y en la cola de la máquina
  (`POST /api/v1/casos/{id}/pasos/{ejecucionPasoId}/cancelar`). A un robot que ya la ejecuta no se le detiene: lo que informe
  después se rechaza con un 409.
- **Acciones masivas** (en el **Listado** de casos, permiso `casos.masivas`; cada acción pide además el suyo,
  `casos.cancelar` para cancelar): el Listado es también la pantalla para aplicar una acción a muchos casos a la vez. Se elige el proceso
  (o varios) con el mismo selector que «Personalizar» del panel —todos marcados, se quitan los que sobran— y las fechas de creación; la
  lista tiene filtros (título, situación), se marcan de uno en uno o todos los que coinciden (también más allá de la página, hasta 1000), y la barra inferior ofrece un botón por cada acción
  disponible. Lo que la acción no admite (un caso ya terminado, de un proceso al que no tienes acceso…) se deja como está y el
  informe final explica por qué. Hoy hay una acción, **cancelar**; con más de 10 casos hay que escribir CANCELAR para confirmar.

### Añadir una acción masiva

Pensado para que una acción nueva sea una pieza, no un cambio en la pantalla ni en el endpoint:

- **Servidor:** una clase que implementa `IAccionMasivaSobreCaso` (`Id`, `Permiso`, `ValidarParametros`,
  `MotivoPorElQueNoAplica`, `EjecutarAsync`) y una línea en `Casos/DependencyInjection.cs`. Responde en
  `POST /api/v1/casos/acciones/{id}` con `{ "ids": [...], "parametros": ... }` (hasta 500 ids) y devuelve
  `{ "procesados": n, "omitidos": [{ "id", "motivo" }] }`. Lo común a todas —que el caso exista y el usuario lo vea, el permiso de la
  acción, que uno que falle no tumbe al resto, el informe— lo hace `EjecutorDeAccionesMasivas`.
- **Cliente:** una entrada en `features/casos/accionesMasivas.ts` (etiqueta, icono, confirmación, la petición, a qué refrescar).
  La pantalla, el botón con su confirmación (`AccionMasivaBoton`) y el informe (`ResultadoMasivoAviso`) son genéricos, y valen para
  cualquier otro listado con casillas: basta otra lista de `AccionMasiva`.
- Ideas que encajan sin tocar nada más: reprocesar fallidos, pausar o reanudar, cambiar la prioridad (usaría `parametros`).

## Prioridad de las ejecuciones

Una **ejecución** es lo que un robot toma de la cola: un paso RPA de un caso esperando a su servicio. Cada una tiene su
prioridad, y hay dos niveles, siempre en este orden:

1. **La máquina** decide qué servicio va primero (el orden o los turnos de arriba).
2. **El servicio** tiene una cola de ejecuciones esperando, y esa cola va por **prioridad**: va primero la de número más
   alto; a igual prioridad, la que lleva más tiempo esperando.

Por eso la prioridad solo ordena la cola de su propio servicio: una ejecución con prioridad 1000 del servicio B no se salta
al servicio A si la máquina pone el A primero.

- Toda ejecución nace con prioridad **0**. Un número mayor va antes; uno negativo espera detrás de las normales. Rango de
  -1000 a 1000.
- **Un robot** puede elegir la de la ejecución que abre al crear un caso:
  `CrearCasoAsync(new CrearCasoRobotRequest("Título", datos, Prioridad: 10))` (librería `Viriato.Rpa.Client` 0.7.0 o
  posterior). Es la del paso por el que empieza el caso; las ejecuciones de los pasos siguientes nacen en 0 como cualquier
  otra, porque cada una es una ejecución nueva.
- **Una persona** la cambia mientras la ejecución espera: en la pestaña *Ejecuciones* del caso, o en la cola de la máquina
  (diálogo *Despacho*), con permiso `casos.prioridad`. Por API:
  `PATCH /api/v1/casos/{casoId}/pasos/{ejecucionPasoId}/prioridad` con `{ "prioridad": 10 }`. Queda anotado en el historial.
- Surte efecto al momento: la cola se lee cada vez que un robot pide trabajo. Una ejecución que un robot ya tiene, o que ya
  terminó, no admite el cambio (la API responde 409): su sitio en la cola ya no significa nada.
- **Reprocesar** una ejecución la devuelve a la cola con la prioridad que tenía.
- La cola de la máquina y la pestaña *Ejecuciones* muestran la prioridad cuando no es 0.

## Tiempo máximo de un servicio

Cada **servicio** puede tener un **tiempo máximo de ejecución** (RPA → Servicios, en minutos). Es la única regla para decidir
que algo se ha colgado: no hay latidos ni señales de vida.

- Un paso está **en ejecución** desde que un robot lo reclama hasta que informa de su resultado.
- Si lleva en ejecución más que el tiempo máximo de su servicio, **se cancela el caso**: el paso queda *Cancelado* con el
  motivo («superó el tiempo máximo de su servicio (N min)»), la ejecución *Cancelada* y el caso *Cancelado* con el estado de
  negocio **«Cancelado por exceso de tiempo de ejecución»** (código `CANCELADO_POR_TIEMPO`, final), distinto del *Descartado* de
  quien lo cancela una persona, para ver de un vistazo que no fue una decisión humana. Ese estado se crea solo en el proceso la
  primera vez que hace falta (y se puede renombrar como cualquier otro). Un proceso de fondo lo comprueba cada 15 s
  (`Despacho:BarridoSegundos`).
- En cuanto se pasa de tiempo, el paso **deja de ocupar el hueco** de la máquina, aunque el barrido todavía no haya llegado a
  él. Así un robot colgado no frena a los demás más allá de ese tiempo.
- Si el robot termina justo en ese instante, gana el primero que llegue (es una única actualización condicional). Si el robot
  informa cuando el caso ya se canceló, la API responde 409 y el caso sigue cancelado.
- El tiempo se cuenta desde que el robot **reclama** el paso, no desde que el caso empezó a esperar.
- Un servicio **sin tiempo máximo** no se cancela nunca por tardar. Es lo que hay por defecto, y tiene un coste: si su robot
  se cae sin avisar, el paso se queda «en ejecución» y **ocupa su hueco para siempre**, hasta que alguien cancele o reprocese
  el caso. Por eso conviene poner un tiempo máximo a todos los servicios (el doble de lo que tardan normalmente).

Qué NO hace: no distingue un robot caído de uno lento. Un robot que muere al minuto 2 de un servicio con 60 min de límite
retiene su hueco 58 minutos. Cuanto más ajustado el tiempo, menos espera; cuanto más holgado, menos riesgo de cancelar un caso
que iba bien.

## Robots que no responden

Para que un robot apagado no frene a los demás:

- Un robot cuenta como **conectado** si ha llamado a la API en los últimos 30 segundos (`Despacho:VentanaConexionSegundos`).
  Un robot libre pregunta por trabajo cada pocos segundos; uno que está ejecutando un paso no necesita preguntar.
- Un robot **desconectado o apagado** no compite por el turno: no hace esperar a los que están por debajo.
- Una **copia** que ejecuta un paso se considera **ocupada**: no recibe otro hasta terminar. Un robot está **ocupado** cuando
  todas sus copias lo están; mientras le quede una libre, compite por el siguiente.

## Límite global de un servicio

Aparte del límite opcional de la máquina, un servicio puede tener un **máximo de ejecuciones simultáneas en todas las máquinas** (para
lo que se comparte, como una cuenta de un portal que no admite dos sesiones). Mientras tenga tantos pasos en ejecución como su
límite, ninguna máquina le da otro.

## Si un robot «no recibe nada»

Abre *Despacho* del equipo y mira la cola de la derecha: dice cuántos hay en uso, qué está esperando y por qué cada cosa
no avanza. Lo habitual: el límite de simultáneas está lleno (quizá por un paso colgado de un servicio sin tiempo máximo), hay otro
servicio por delante en el orden, o el robot del servicio que debería trabajar está apagado o sin conexión. Para soltar un
hueco ocupado por un paso colgado, cancela su caso.

## API

Todas requieren el permiso `rpa.manage`.

| | |
|---|---|
| `GET /api/v1/equipos/{id}/despacho` | Configuración del equipo y los servicios que ejecuta. |
| `PUT /api/v1/equipos/{id}/despacho` | `{ maxEjecucionesSimultaneas: número\|null, politica: "Prioridad"\|"Turnos", orden: [servicioId…] }`; reemplaza todo (`null` = sin límite). |
| `GET /api/v1/equipos/{id}/despacho/cola` | Qué se ejecuta y qué espera, en orden, y los robots con sus copias (`robots`: `instancias`, `libres`). |
| `POST /api/v1/equipos/{id}/despacho/plantilla/{plantillaId}` | Copia una plantilla al equipo. |
| `GET/POST /api/v1/plantillas-despacho`, `PUT/DELETE …/{id}` | Gestión de plantillas. |
