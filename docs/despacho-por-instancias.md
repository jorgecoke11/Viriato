# Despacho por copias del robot: la capacidad la pone el stack

> Para el mapa completo con diagramas, empieza por [despachador-esquema.md](despachador-esquema.md).

**El problema.** Si un RPA coge un caso y no termina, el siguiente tenía que esperar a que ese servicio acabase (o a que se
agotase su tiempo máximo). Subir réplicas del robot no ayudaba: para la plataforma todas las copias de un despliegue eran **un
solo robot**, ocupado mientras una tuviera un paso. Y encima la puerta la ponía un número que se configuraba en el front
(«ejecuciones simultáneas» de la máquina, por defecto 1).

**Lo que se quiere.** Que la capacidad salga de **cuántas copias del robot se ejecutan**: más réplicas en el stack (contenedores)
o más procesos arrancados en Windows con la misma clave. Que una copia atascada solo ocupe **su** hueco. Y que la prioridad se
cumpla igual.

## El modelo

- Un **robot** (despliegue) puede ejecutarse como varias **copias**. Cada copia se identifica sola: al arrancar genera un id
  (`RpaClientOptions.InstanciaId`, por defecto uno por proceso) y lo manda en la cabecera `X-Viriato-Instancia` en cada
  petición. La plataforma no lo configura ni lo cuenta en ningún sitio: **aparece cuando pide trabajo**.
- Una copia solo pide trabajo cuando está libre. Por tanto, una copia que pide es un hueco libre; una copia con un paso en
  ejecución (reclamado, sin terminar y dentro del tiempo máximo de su servicio) está **ocupada**. No hay latidos: lo que se sabe de
  cada copia sale de que pregunta y de los pasos que tiene.
- Un robot **compite por el siguiente paso** si tiene alguna copia libre. Una copia atascada no frena a las demás, ni al resto de
  robots.
- Una copia que muere deja su paso reclamado hasta que se agota el tiempo máximo del servicio; entonces la plataforma cancela ese
  caso (y solo ese). Mientras tanto el resto de copias siguen trabajando. Una copia nueva llega con otro id y trabaja al momento.
- Un robot que **no manda id** (un cliente anterior) cuenta como **una copia**, igual que se comportaba antes.

## Cómo se decide un reclamo

`POST /api/v1/rpa/cola/siguiente`, bajo el cerrojo de despacho (una decisión a la vez en toda la plataforma):

1. Se apunta la copia (despliegue + id + hora) para que el resto de robots de la máquina sepan que existe.
2. Si la copia ya tiene un paso en ejecución → nada (204).
3. Si el servicio está al **límite global** (máximo de ejecuciones del servicio en todas las máquinas) → nada.
4. Si la máquina tiene **tope opcional** y ya lo alcanzó → nada.
5. **Sin tope** (lo normal): la copia recibe el primero de la cola de su robot: la ejecución con mayor prioridad y, a igualdad, la
   que lleva más esperando. **Con tope**: los robots de la máquina se turnan según su orden de servicios y política, como antes.

La **prioridad** se mantiene porque la decisión es atómica y ordenada: varias copias que preguntan a la vez reciben la cola
estrictamente en orden (hay un test con 4 copias y 6 ejecuciones de prioridades distintas).

## El tope por máquina pasa a ser opcional

Antes, `Equipo.MaxEjecucionesSimultaneas` era obligatorio (por defecto 1) y era lo que impedía que las copias trabajasen. Ahora
es **opcional y vacío por defecto**:

- **Vacío** (máquinas nuevas): no hay límite; mandan las copias que haya. En este modo cada robot trabaja su propia cola, así que
  el **orden de servicios y la política** de la máquina no intervienen (solo importan cuando varios robots compiten por un hueco
  que tiene tope).
- **Con un número**: se comporta exactamente como antes (los robots se turnan según el orden). Es lo que hay que usar si varios
  robots comparten pantalla o navegador y no pueden ir a la vez.

Las máquinas que ya existían **conservan su valor**: la migración no lo toca. Para que sus copias trabajen en paralelo hay que
vaciar el campo en *Despacho* de la máquina (un solo clic).

> **Decisión tomada (cámbiala si no te encaja):** no se quita el tope del todo porque hay robots que manejan la pantalla y
> necesitan ir de uno en uno; el tope sigue siendo la forma de pedirlo. Si prefieres que **nunca** haya tope por máquina, se retira
> el campo (y con él el orden y la política, que solo existen para repartir un hueco escaso).

## Qué se ve

En *Despacho* → cola de la máquina:

- Si no hay tope: «Sin límite: la capacidad son las copias de los robots. N libres ahora.»
- La lista **Robots y sus copias**: por cada robot cuántas copias tiene y cuántas están libres (o «Sin copias en marcha» /
  «Apagado»).

## Cómo escalar

- **Contenedores:** `docker compose up -d --scale <robot>=3` (o `replicas: 3` en el stack). Todas las réplicas usan la misma
  `Viriato__ApiKey`; cada una genera su id al arrancar.
- **Windows:** arranca otro proceso del robot con la misma clave. Cada proceso tiene su id. Si un mismo proceso aloja varios robots,
  da a cada uno un `Viriato:InstanciaId` distinto.
- **Qué servicio atiende cada copia** lo decide la clave de despliegue que lleva en su propia configuración (el stack), no una
  pantalla.

## El barrido de tiempos máximos con varias réplicas de la API

El barrido (`VigilanteDePasos`) que cancela los pasos que pasan su tiempo máximo toma un cerrojo (`pg_try_advisory_xact_lock`): con
varias réplicas de la API solo una barre en cada vuelta. Aunque dos coincidieran no habría daño: cada cancelación es una
actualización condicional que solo gana una. De paso borra las copias que llevan un día sin preguntar.

## Cambios en el cliente de los robots

`Viriato.Rpa.Client` manda la cabecera de la copia automáticamente. Los robots que **no actualicen** siguen funcionando como una sola
copia. Para aprovechar las réplicas hay que publicar y adoptar la versión con este cambio (la 0.7.0 del cliente y la 1.4.0 de los
contratos aún no se han publicado, así que van juntas).

## Lo que no cambia

El tiempo máximo por servicio, el límite global por servicio, las prioridades por ejecución, y la política y orden de la máquina
cuando se pone un tope.

## Pendiente de decidir contigo

- _(Resuelto)_ El tiempo máximo agotado deja el caso en el estado de negocio «Cancelado por exceso de tiempo de ejecución».
- Si prefieres retirar el tope por máquina del todo (ver arriba).
