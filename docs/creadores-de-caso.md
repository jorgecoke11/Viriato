# Creadores de caso: crear un caso eligiendo qué crear

Para quien usa la plataforma, crear un caso es **elegir un creador y rellenar sus datos**. El flujo, el tipo de caso, el servicio por
el que empieza, el estado de negocio inicial y cómo se escribe el título ya vienen configurados en el creador.

## Qué es un creador

Una entidad del **proceso** (`flujos.creadores_de_caso`), con tantos como haga falta («Alta de cliente», «Alta urgente»…):

| Campo | Para qué |
|---|---|
| Nombre, descripción | Lo que ve el usuario al elegir. |
| Tipo de caso | El tipo que recibe el caso **y**, con él, el formulario de datos (el esquema del tipo). Sin tipo: JSON libre. |
| Servicio por el que empieza | Un paso del proceso, **por nombre** (así sigue valiendo al publicar versiones nuevas). Los anteriores quedan como omitidos. Vacío: el primer paso. |
| Estado de negocio inicial | El estado con el que nace el caso. |
| Título automático | Plantilla del título cuando el usuario no escribe uno. |
| Orden, activo | Cómo se ordena y si se ofrece. Un creador retirado no se borra de ningún sitio. |

### La plantilla del título

Entre llaves: `{creador}`, `{proceso}`, `{tipo}`, `{fecha}` (2026-10-08), `{hora}` (14:30), `{n}` y `{datos.campo}` (un campo de los datos
del caso; `{datos.cliente.nombre}` baja por los objetos). Un campo que el usuario dejó vacío desaparece sin dejar huecos. La
predeterminada es `{creador} {fecha} #{n}`. Al guardar se rechaza una errata (`{fehca}`).

`{n}` es un contador **por creador** que mueve la base de datos con un solo `update … returning`, así que dos personas creando a la
vez nunca comparten número. Si el caso se rechaza después, queda un hueco en la numeración (mejor que repetir).

El usuario **puede escribir su propio título** (el id de negocio): entonces se usa tal cual y no se gasta número.

## Cómo se usa

- **Configurar** (quien gestiona el proceso, `flujos.manage`): pestaña **Creadores** del proceso (*Procesos → el proceso*).
- **Crear un caso** (`casos.crear`): *Listado → Nuevo caso*, o el «+» de la tarjeta de un proceso en el Panel. Si el usuario solo
  tiene un creador, ya viene elegido. El título se muestra como ejemplo («Alta de cliente 2026-10-08 #7») para que nadie se pregunte
  qué saldrá.
- **Sin creador** (avanzado, `casos.manage`): *Nuevo caso → Crear sin creador*. El formulario completo de siempre, para quien gestiona
  y necesita arrancar algo a mano o aún no ha configurado ninguno.

## API

| | |
|---|---|
| `GET /api/v1/flujos/{flujoId}/creadores` (`flujos.read`) | Los creadores del proceso. |
| `POST` / `PUT /…/creadores[/{id}]`, `DELETE /…/creadores/{id}` (`flujos.manage`) | Crear, **reemplazar entero** (así se puede vaciar una referencia) y borrar. Se comprueba que tipo, estado y paso existen en el proceso. |
| `GET /api/v1/creadores-de-caso` (`casos.crear`) | Los que **puedo usar**: activos, de procesos que tengo asignados y con versión publicada; con el esquema del tipo y un título de ejemplo. |
| `POST /api/v1/creadores-de-caso/{id}/casos` (`casos.crear`) | `{ titulo?, datosJson? }`. Pasa por **las mismas comprobaciones** que crear un caso a mano (asignación al proceso, esquema de datos…): solo pregunta menos. |

Un creador de otro usuario, retirado o de un proceso sin asignar responde igual que uno que no existe (404): nadie descubre cuáles hay.

## Ampliar

Lo que el creador decide es un conjunto cerrado de campos de `StartCasoRequest`. Para que un creador decida algo más (datos por defecto,
prioridad…) se añade una columna a `CreadorDeCaso` y se vuelca en `CreadoresDeCasoUsoEndpoints.CrearCasoAsync`; el resto no cambia.
