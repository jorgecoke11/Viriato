# Transiciones entre servicios: servicio + estado de ejecución + estado de negocio → siguiente servicio

> **Implementado** en la rama `feat/estados-automaticos`. Diferencias con lo que sigue:
> - No hay marca `ModeloHeredado`: **una versión sin flechas es la antigua** y sigue avanzando por `Orden` (con `Decision`), así que la
>   migración no convierte datos. El editor convierte un borrador antiguo a flechas al abrirlo (Inicio → primero, cada paso → el
>   siguiente con *Completada*), salvo si tiene pasos `Decision`, que se sigue viendo como lista; las versiones publicadas o archivadas
>   sin flechas también se ven como lista.
> - `Orden` y `ConfiguracionJson` siguen en la API para esas versiones antiguas; con flechas, la configuración de los Rpa se descarta.
> - El editor de flechas se carga aparte (`lazy`) y no hay minimapa.

Escrito sobre: fa2b30c3115889e7b55c0d41365b77ac41fa3e49

## Qué se quiere

El flujo de un proceso decide **qué servicio trabaja el caso a continuación** con reglas como esta:

> Cuando el servicio **BSH - Extractor de precios** termina con la ejecución **Completada** y el caso en el estado de negocio
> **`PRECIOS_EXTRAIDOS`** → lanzar una ejecución del servicio **BSH - Actualizador de precios**.

Cada regla es una **transición**. En el editor de la versión, los servicios son cajas y las transiciones, flechas entre ellas. Al pulsar una flecha
se elige el estado de ejecución y el estado de negocio. Si al terminar una ejecución no se cumple ninguna regla, el caso se cierra con
el resultado de esa ejecución.

La configuración JSON de los pasos Rpa (`{"aplicacion": "…"}`) desaparece: los robots no la usan. Lo comprobado: ni
`Bsh.CasesCreator`, ni `Bsh.PricesExtractor`, ni `Bsh.PricesUpdater` leen `AplicacionObjetivo` o `ParametrosEntrada`.

## Hoy

- Los pasos son una lista (`FlujoPasoDef.Orden`) y el caso avanza siempre al `Orden + 1`
  (`backend/src/Viariato.Modules.Casos/Orchestration/EjecucionOrchestrator.cs:429`). Para bifurcar existe el paso `Decision`, que lee un
  booleano de los datos del caso y marca como `Omitido` un único paso de la otra rama (:447-497, :553).
- Si un paso falla, el caso se cierra como fallido siempre (`AvanzarAsync`, :94-96).
- Un paso Rpa apunta a un servicio (`FlujoPasoDef.ServicioId`) y lleva `ConfiguracionJson`, de donde `RpaPasoEjecutor` saca
  `AplicacionObjetivo` y `ParametrosEntrada` (`Orchestration/Ejecutores/RpaPasoEjecutor.cs:19-41`). Además, el orquestador puede aplicar
  un estado de negocio leyendo `estadoNegocioCodigo` de esa configuración (:506-520).
- Los robots ya ponen el estado de negocio **antes** de devolver Completado/Fallido. Por ejemplo, el extractor pone
  `PRECIOS_EXTRAIDOS` y después completa. Así que, cuando el paso termina, el estado del caso ya es el que dejó el robot.

## Modelo

### Nodos: los servicios de la versión

Se mantiene `FlujoPasoDef` como nodo para no tocar lo que ya cuelga de él (`EjecucionPaso.FlujoPasoDefId`, reprocesamientos,
creadores, familias), pero en un paso Rpa **el nodo es el servicio**:

- **Un servicio aparece una sola vez por versión.** Se valida al guardar. Así "servicio X" identifica sin ambigüedad el nodo de origen
  de una regla.
- `Nombre` del paso: por defecto, el del servicio. Se puede cambiar, porque los creadores (`PasoInicialNombre`) y las familias
  (`PasoQueLaForma`) lo buscan por nombre.
- `ConfiguracionJson` **se elimina para los pasos Rpa**: el editor no lo muestra y el validador lo rechaza si llega. Los demás tipos
  (Api, Agente, Espera, RevisionHumana, Interno) lo conservan, porque su ejecutor sí lo usa.
- `Orden` queda solo como orden de presentación (desplegables y listas). Deja de decidir el avance.
- Nuevas columnas `PosicionX`, `PosicionY` (`double?`): dónde se pinta la caja. Solo es presentación.

### Transiciones: `FlujoTransicionDef` (nueva, tabla `flujos.transiciones`)

Pertenece a la versión y se congela con ella al publicar.

| Campo | Tipo | Significado |
|---|---|---|
| `Id` | Guid | |
| `FlujoVersionId` | Guid | Se borra con la versión. |
| `PasoOrigenId` | Guid? | El servicio (paso) que acaba de terminar. Si es `null`, es **Inicio**: el servicio con el que empieza un caso nuevo. |
| `ResultadoEjecucion` | enum `Completada`/`Fallida` | Cómo terminó la ejecución de ese servicio. Se ignora si el origen es Inicio. |
| `EstadoNegocioId` | Guid? | El estado de negocio en el que tiene que estar el caso (`FlujoEstadoDef`). Si es `null`, vale **cualquier estado**. |
| `PasoDestinoId` | Guid | El servicio (paso) al que se lanza la siguiente ejecución. |

Restricciones:
- Única por `(FlujoVersionId, PasoOrigenId, ResultadoEjecucion, EstadoNegocioId)`, con los `null` tratados como un valor más (índice
  único con `NULLS NOT DISTINCT` en PostgreSQL 15+, o una columna calculada). Así, para una misma combinación hay como mucho un destino.
- Exactamente una transición desde Inicio.

### Regla al terminar una ejecución

Cuando la ejecución de un servicio termina como `Completado` o `Fallido`:

1. Se aplica el estado de negocio, como hoy (`AplicarEstadoNegocioSiCorrespondeAsync`).
2. Se buscan las transiciones con origen = ese paso y resultado = el de la ejecución.
3. Gana la que tiene **exactamente** el estado de negocio actual del caso. Si no la hay, gana la que tiene **cualquier estado**
   (`EstadoNegocioId = null`).
4. Si hay ganadora, se crea y se despacha una ejecución del servicio destino. Se reutiliza `CrearYDespacharPasoAsync` con la cuenta de
   `intentosPrevios` que hay hoy (:440-444), así que volver a un servicio ya visitado es su intento N+1.
5. **Si no hay ninguna**, el caso se cierra con el resultado de la ejecución: `Completada/Completado` o `Fallida/Fallido`. Es lo que pasa
   hoy en el último paso y en cualquier fallo.

`EsperandoRevisionHumana` no cambia: al aprobar o rechazar, el paso termina como `Completado`/`Fallido` (:327) y se aplica la misma
regla.

**Las reglas no son retroactivas.** Una transición solo se evalúa **en el momento en que termina una ejecución**. Nada recorre
los casos buscando "los que están en el estado X": un caso que ya está cerrado en `PRECIOS_EXTRAIDOS` no se mueve cuando se publica una
versión con la regla `Extractor · Completada · PRECIOS_EXTRAIDOS → Actualizador`. Además, cada caso sigue atado a **su** versión, así que
la regla nueva solo la ven los casos creados con la versión nueva. La única forma de que un caso antiguo la alcance sería **reprocesar** su
paso, y aun así seguiría las reglas de su propia versión (la migrada, que es lineal). Hay que cubrirlo con un test: caso cerrado en
`PRECIOS_EXTRAIDOS` con la versión anterior, se publica la versión con la regla y el caso no genera ninguna ejecución.

**Bucles:** se permiten (por ejemplo, Fallida → el mismo servicio para reintentar). Cada `FlujoPasoDef` tiene `MaximoDeVisitas`
(`int?`; si es `null`, vale 10). Si una transición iba a crear una ejecución por encima de ese número dentro de la misma `Ejecucion`, el
caso se cierra como fallido con el evento "Demasiadas vueltas en <servicio>".

### Lo que desaparece

- **Paso `Decision`:** el editor deja de ofrecerlo. Lo sustituyen dos flechas con estados de negocio distintos. El ejecutor y el valor
  del enum se conservan solo para que la migración pueda leer las versiones heredadas (ver Migración).
- **`Omitido` para la rama no tomada**, `EncontrarSiguientePasoNoOmitidoAsync` y `MarcarRamaOmitidaAsync`: con transiciones no hay
  "siguiente por orden" que saltar. Se borran después de migrar.
- **`ConfiguracionJson` en los pasos Rpa.** `RpaPasoEjecutor` rellena `AplicacionObjetivo` con el **nombre del servicio** (el campo es
  obligatorio en `RpaEjecucionDetalle` y en el contrato `EjecucionAsignadaDto`, que no se cambia, para no romper robots publicados) y deja
  `ParametrosEntrada` a `null`. Los parámetros de verdad siguen en la pestaña Parámetros del proceso (`ObtenerParametrosAsync`).
- **`estadoNegocioCodigo` en la configuración de los pasos Rpa:** al no haber configuración, deja de aplicarse. Si algún proceso lo
  usa, la migración lo detecta y lo avisa (ver Migración). En los demás tipos de paso se mantiene.

## Lo que hoy depende de `Orden` y cómo cambia

| Sitio | Hoy | Con transiciones |
|---|---|---|
| `IniciarCasoAsync` (:19-73) | Sin paso inicial, el `Orden` más bajo. Si se arranca más adelante, marca `Omitido` los anteriores. | Sin paso inicial, el destino de la transición desde **Inicio**. Si se arranca en otro (creadores con `PasoInicialNombre`), se registra el evento "Empezó en <servicio>" y no se crean filas `Omitido`. |
| `ContinuarTrasCompletarAsync` (:416-445) | `Orden + 1`, o la rama de `Decision` | La regla de arriba. |
| `AvanzarAsync`, rama `Fallido` (:94-96) | Cierra el caso | Aplica el estado de negocio y la regla con `ResultadoEjecucion = Fallida`. Si no hay regla, cierra el caso como ahora. |
| `ReprocesarPasoAsync` (:113-151) | Vuelve a ejecutar el paso y luego sigue por `Orden` | Vuelve a ejecutar el paso y luego sigue las transiciones. El código no cambia. |
| Creadores, `PasoInicialNombre` (`RpaWorkerEndpoints.cs:197-205`) | Paso por nombre, o el de `Orden` mínimo | Por nombre, o el destino de Inicio. |
| Reglas de reprocesamiento, lista de pasos (`ReprocesamientosEndpoints.cs:84-92`) | Nombres por `Orden` | Igual. |
| Familias, `PasoQueLaForma` (`FamiliasEndpoints.cs:134-157`) | Por nombre | Sin cambios. |

## API

`PUT /api/v1/flujos/{flujoId}/versiones/{versionId}/pasos` (el reemplazo completo de `FlujoVersionesEndpoints.cs`) pasa a reemplazar
**pasos y transiciones** juntos, en una transacción y solo si la versión está en Borrador. Las transiciones referencian a los pasos por
una **clave del cliente**, porque los nuevos aún no tienen Id:

```json
{
  "pasos": [
    { "clave": "crear",    "orden": 1, "nombre": "Crear casos",        "tipoPaso": "Rpa", "servicioId": "…creador…",      "posicionX": 0,   "posicionY": 0 },
    { "clave": "extraer",  "orden": 2, "nombre": "Extraer precios",    "tipoPaso": "Rpa", "servicioId": "…extractor…",    "posicionX": 280, "posicionY": 0 },
    { "clave": "subir",    "orden": 3, "nombre": "Actualizar precios", "tipoPaso": "Rpa", "servicioId": "…actualizador…", "posicionX": 560, "posicionY": 0 }
  ],
  "transiciones": [
    { "origen": null,      "destino": "crear" },
    { "origen": "extraer", "resultado": "Completada", "estadoNegocioCodigo": "PRECIOS_EXTRAIDOS", "destino": "subir" }
  ]
}
```

El estado de negocio va por **código** en la API (`estadoNegocioCodigo`, o `null` para cualquiera). El servidor lo resuelve a
`EstadoNegocioId` del proceso. `GET` de la versión devuelve lo mismo con los `Id` reales.

### Validación

Bloquean el guardado:
- Cada servicio aparece una sola vez en la versión, y todo paso Rpa tiene servicio.
- Hay exactamente una transición desde Inicio, y es la única sin `resultado`.
- `origen`/`destino` son claves de la petición. El estado de negocio existe y está activo en el proceso.
- No hay dos transiciones con el mismo `(origen, resultado, estado)`.
- Los pasos Rpa no llevan `configuracionJson`.

Solo avisan (la respuesta las devuelve como `avisos`, y el lienzo las marca en la caja):
- Un servicio **al que no se llega** desde Inicio.
- Un bucle sin `MaximoDeVisitas` explícito.

Al **publicar**, un servicio inalcanzable bloquea.

## Migración

1. **Esquema** (`AddFlujoTransiciones`): tabla `flujos.transiciones`, y en `flujo_pasos` las columnas `PosicionX`, `PosicionY` y
   `MaximoDeVisitas`.
2. **Datos.** Para cada versión existente (borrador, publicada y archivada) se generan transiciones que reproducen el comportamiento
   de hoy:
   - Inicio → paso con `Orden` mínimo.
   - Paso no `Decision` → `Completada` + cualquier estado → paso `Orden + 1`. El último no tiene flecha, así que el caso se completa. Los
     fallos no tienen flecha, así que el caso se cierra como fallido, como hoy.
   - **Versiones con `Decision`**: su condición es un dato booleano y las transiciones solo saben de estados de negocio, así que no se
     pueden traducir 1:1. Esas versiones **se marcan como heredadas** (`FlujoVersion.ModeloHeredado = true`) y el orquestador las sigue
     moviendo con el código de hoy (`Orden + 1`, `Decision`, `Omitido`) hasta que no quede ningún caso en curso con ellas. El editor las
     muestra en solo lectura con el aviso "Versión con decisiones: crea una versión nueva para pasarla a transiciones".
   - **Servicio repetido** dentro de una misma versión: también queda como heredada, por la misma razón.
   - **`estadoNegocioCodigo` en la configuración de un Rpa**: se conserva en las versiones heredadas. En las migradas, se deja en
     `ConfiguracionJson`, que ya no se lee, y se escribe un aviso en el log de la migración con el proceso, la versión y el paso, para
     moverlo al robot.
   - Posiciones: en fila, `x = (Orden - 1) * 280`, `y = 0`.
3. **Casos en curso:** siguen en su versión. Si esta se ha migrado, avanzan con transiciones equivalentes. Si es heredada, con el código
   antiguo.

## Frontend

### Librería

`@xyflow/react` (React Flow 12), con una versión exacta en `package.json`. Ofrece cajas personalizadas, conexiones arrastrando, zoom y
desplazamiento, minimapa y borrado con teclado. Funciona con React 18 y no hay otra librería de grafos en el proyecto.

### Pantalla: `FlujoTransicionesEditor.tsx`

Sustituye a `FlujoPasosEditor.tsx` y se abre desde el mismo botón **Editar pasos / Ver pasos** de `FlujoVersionesPanel.tsx:107`.

- **Paleta** (izquierda): la lista de **servicios activos** (`rpaApi.listServicios()`, que ya usa el editor actual). Se arrastran al
  lienzo. Los que ya están en la versión aparecen deshabilitados. Debajo, los otros tipos (Revisión humana, Api, Agente, Espera,
  Interno), que siguen teniendo su configuración. Inicio está siempre en el lienzo y no se puede borrar.
- **Lienzo** (centro):
  - **Caja de servicio:** nombre del paso y, debajo en gris, el servicio. Tiene una asa de entrada y una de salida. Arrastrar de la
    salida de una caja a la entrada de otra crea la flecha y abre su configuración.
  - **Flecha:** verde si es *Completada* y roja si es *Fallida*. La etiqueta es el estado de negocio (`PRECIOS_EXTRAIDOS`, o
    *cualquier estado*). Puede haber varias flechas entre las mismas dos cajas, con estados distintos, y se pintan separadas.
  - Los avisos marcan la caja con un borde ámbar y un tooltip.
- **Panel de propiedades** (derecha), según la selección:
  - **Flecha:** *Cuando termina* (Completada / Fallida), *con el caso en* (desplegable con los estados de negocio activos del proceso, más
    *Cualquier estado*). Si la combinación ya existe desde esa caja, se avisa allí mismo.
  - **Caja de servicio:** nombre del paso, servicio (solo lectura; para cambiarlo, se borra y se arrastra otro) y máximo de vueltas.
  - **Caja de otro tipo:** sus campos de hoy (agente, configuración JSON…).
  - **Nada seleccionado:** la lista de reglas en texto ("Extractor · Completada · PRECIOS_EXTRAIDOS → Actualizador"), útil para
    revisarlas de un vistazo, y los avisos.
- **Barra superior:** Guardar, **Ordenar** (colocación por niveles desde Inicio, de izquierda a derecha, con un algoritmo propio
  simple, sin añadir `dagre`) y Volver. Las versiones publicadas, archivadas o heredadas se ven en solo lectura.
- Salir con cambios sin guardar pide confirmación. No se guarda nada hasta pulsar Guardar, como hoy.

### Datos

`features/flujos/api.ts`: `FlujoTransicionDto`, `GrafoVersionDto` y `reemplazarGrafo(flujoId, versionId, grafo)`. Los estados de
negocio se cargan con la consulta que ya usa `FlujoEstadosPanel.tsx`.

## Cómo queda el P01 de BSH

| Desde | Termina | Caso en | → Servicio |
|---|---|---|---|
| Inicio | | | BSH - Creador de casos |
| BSH - Extractor de precios | Completada | `PRECIOS_EXTRAIDOS` | BSH - Actualizador de precios |

- El caso *Creador de casos*, al completarse, no tiene flecha y se cierra. Los casos que crea arrancan en *Extraer precios* por
  `Creador:PasoInicial`, como hoy.
- Un caso de extracción con `actualizar: false` (o sin él) no pasa al actualizador: el extractor lo deja en
  `PRECIOS_EXTRAIDOS_SIN_ACTUALIZAR`, que no tiene flecha, así que el caso se cierra completado. **Hecho en el repo de los robots**
  (`PreciosHandler`, `Precios:Estados:CompletadaSinActualizar`). No hace falta crear el estado: Viriato crea el estado de negocio la
  primera vez que un robot lo usa (`EstadosDeNegocio.ObtenerOCrearAsync`). El chequeo de `actualizar` en `ActualizadorHandler` se queda
  como segunda barrera.
- Como los estados se crean solos, el desplegable de estados del editor de flechas solo muestra los que ya se han usado o creado a
  mano. Para poder dibujar una flecha antes de que el robot haya pasado por ese estado, el desplegable acepta también **escribir un código
  nuevo**, que se crea al guardar la versión.
- Si se quiere: Extractor · Fallida · `ERROR_EXTRACCION` → un paso de Revisión humana.

## Fuera de alcance

- Lanzar **varios servicios a la vez** desde una misma regla, o esperar a varios: la ejecución tiene un único `PasoActualId`.
- Condiciones sobre los **datos** del caso: se expresan con estados de negocio, que pone el robot.
- Pintar en el lienzo **el recorrido de un caso concreto** (desde el detalle del caso). Es la mejora natural siguiente.

## Orden de trabajo y verificación

1. **Modelo y migración:** entidad, configuración de EF, migración de esquema y de datos, y la marca de heredada. Tests de la conversión:
   lineal, con `Decision` (queda heredada) y con un servicio repetido (queda heredada).
2. **Orquestador:** regla al terminar (Completado y Fallido), Inicio en `IniciarCasoAsync`, `MaximoDeVisitas`, `RpaPasoEjecutor` sin
   configuración, y la rama heredada intacta. Tests: lineal, por estado exacto, prioridad del estado exacto sobre *cualquiera*, fallo hacia
   revisión humana, sin regla (Completado y Fallido), bucle hasta el límite, versión heredada que avanza igual que antes.
3. **API y validación:** `PUT/GET`, códigos de estado, avisos y bloqueo al publicar.
4. **Frontend:** `@xyflow/react`, `FlujoTransicionesEditor` y el cambio en `FlujoVersionesPanel`. Hay que comprobarlo en el navegador:
   montar el P01 de la tabla de arriba, guardar, recargar (se conservan las posiciones y las flechas), publicar y ver la versión
   publicada en solo lectura.
5. **De punta a punta:** un caso de extracción que acaba en `PRECIOS_EXTRAIDOS` entra en la cola del actualizador, y uno que acaba en otro
   estado se cierra sin pasar por él.
6. **Documentación y robots:** README de los robots BSH (*Preparar Viriato*, punto 2: sin configuración JSON y con la tabla de
   transiciones), y en ese repo, el estado `PRECIOS_EXTRAIDOS_SIN_ACTUALIZAR` en el extractor.

## Decisiones abiertas

1. **Versiones heredadas** (las que tienen `Decision` o un servicio repetido): el plan las deja funcionando con el código antiguo hasta
   que no les queden casos. La alternativa es convertirlas a mano antes de desplegar y borrar el código antiguo de una vez. ¿Hay alguna
   en uso?
2. **Límite de vueltas por defecto (10):** ¿vale?
