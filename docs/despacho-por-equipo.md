# Despacho por equipo: quién va primero

Varios robots (despliegues) pueden vivir en la misma máquina. Antes, cada uno cogía lo más antiguo de **su** cola sin
mirar a los demás, así que dos robots del mismo equipo podían trabajar a la vez, sin orden. Ahora el equipo decide.

## Qué se configura

Por cada **equipo** (RPA → Equipos → botón *Despacho*):

| Ajuste | Qué hace |
|---|---|
| **Ejecuciones simultáneas** | Cuántos pasos puede estar ejecutando a la vez la máquina. Por defecto **1**: los robots van uno detrás de otro (lo seguro si usan la pantalla o el mismo navegador). |
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

1. Si la máquina ya está ejecutando tantos pasos como su límite, la respuesta es «nada ahora» (204, igual que una cola vacía).
2. Si no, mira qué tiene esperando **cada robot inactivo y conectado** de la máquina y elige el primero según la regla:
   mejor posición en el orden (o en la rotación) y, a igualdad, lo que lleva más tiempo esperando.
3. Solo ese robot recibe el paso. Los demás reciben «nada ahora» y vuelven a preguntar a los pocos segundos.

## Tiempo máximo de un servicio

Cada **servicio** puede tener un **tiempo máximo de ejecución** (RPA → Servicios, en minutos). Es la única regla para decidir
que algo se ha colgado: no hay latidos ni señales de vida.

- Un paso está **en ejecución** desde que un robot lo reclama hasta que informa de su resultado.
- Si lleva en ejecución más que el tiempo máximo de su servicio, **se cancela el caso**: el paso queda *Cancelado* con el
  motivo («superó el tiempo máximo de su servicio (N min)»), la ejecución *Cancelada* y el caso *Cancelado* con el estado de
  negocio *Descartado*, igual que si lo hubiera cancelado una persona. Un proceso de fondo lo comprueba cada 15 s
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
- Un robot que ejecuta un paso se considera **ocupado**: no compite por otro hasta terminar.

## Límite global de un servicio

Aparte del límite de la máquina, un servicio puede tener un **máximo de ejecuciones simultáneas en todas las máquinas** (para
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
| `PUT /api/v1/equipos/{id}/despacho` | `{ maxEjecucionesSimultaneas, politica: "Prioridad"\|"Turnos", orden: [servicioId…] }`; reemplaza todo. |
| `GET /api/v1/equipos/{id}/despacho/cola` | Qué se ejecuta y qué espera, en orden. |
| `POST /api/v1/equipos/{id}/despacho/plantilla/{plantillaId}` | Copia una plantilla al equipo. |
| `GET/POST /api/v1/plantillas-despacho`, `PUT/DELETE …/{id}` | Gestión de plantillas. |
