# Formularios: los datos de un caso

Un **formulario** describe los datos de negocio de un caso: qué campos tiene, cuáles son obligatorios y qué admite cada uno. Es una
entidad del **proceso** (`flujos.formularios`, pestaña **Formularios**), y lo eligen sus **creadores**: cada creador usa el suyo y
varios pueden compartir uno («Alta» y «Alta urgente»), así que un cambio se hace una vez.

Antes el formulario vivía en el **tipo de caso**. Ya no: el tipo solo clasifica el caso (como el estado de negocio, pero fijo).

## Qué es un formulario

| Campo | Para qué |
|---|---|
| Nombre, descripción | Para elegirlo en los creadores. Nombre único en el proceso. |
| Esquema | El subconjunto de JSON Schema que la plataforma sabe dibujar y comprobar (`EsquemaDatos` en el servidor, `components/schema-form/esquema.ts` en el navegador, con las mismas reglas). Se guarda como texto: el orden de `properties` es el orden de los campos. |

Se edita con el editor de siempre (esquema a la izquierda, vista previa a la derecha, «Generar desde un ejemplo», «Plantilla»).

- **Un formulario que usa algún creador no se puede borrar** (el mensaje dice cuáles). Uno que solo recuerdan casos sí: esos casos
  conservan sus datos y pasan a JSON libre.
- **Cambiar un formulario** se rechaza si dejaría inválidos los valores por defecto de algún creador que lo usa (el mensaje dice cuál).

## Valores por defecto

Hay dos sitios, separados:

- **En el creador** (`DatosPorDefectoJson`): con lo que **empieza el formulario** cuando una persona crea un caso a mano, y la **base**
  sobre la que los lanzadores (y los robots que nombran el creador) ponen sus datos. Se comprueban contra el formulario **sin pedir los
  obligatorios** (modo parcial): un valor por defecto puede dejar el producto para que lo escriba quien crea el caso; lo que sí pone
  tiene que valer.
- **En el lanzador** (`DatosJson`): se escriben con el formulario del creador, empezando por los valores del creador, y **se guarda solo
  lo que cambia**. Así, si mañana cambia el valor por defecto del creador, llega a todo lo que el lanzador no tocó. Un objeto abre un
  caso; una lista («Un caso por fila») abre un caso por elemento, como siempre.

### Cómo se juntan (campo a campo)

`MezclaDeDatos` (servidor) y `mezclarDatos` (navegador): lo que trae el lanzador **gana**; lo que no trae lo pone el creador. Los
objetos se mezclan igual hacia dentro; cualquier otro valor —una lista incluida— se sustituye entero. Un `null` explícito vacía el
campo. Con una lista, cada elemento se mezcla por separado con los valores del creador.

## Qué comprueba el servidor al crear un caso

| Quién | Datos que valen | Contra qué se comprueban |
|---|---|---|
| **Persona con creador** (`POST /creadores-de-caso/{id}/casos`) | Lo que envía tal cual: los valores del creador ya estaban en el formulario y un campo que la persona vació se queda vacío. | El formulario del creador, completo. |
| **Persona sin creador** (*Crear sin creador*, `POST /casos`) | Lo que envía. | El formulario que elija (`formularioId`, opcional, del mismo proceso); sin él, JSON libre. |
| **Lanzador** | Valores del creador + datos del lanzador (por elemento si es lista). | El formulario del creador, completo: **al guardar** el lanzador y **otra vez al disparar** (el formulario pudo cambiar). |
| **Robot** (`CrearCasoRobotRequest`) **con `Creador`** | Valores del creador + datos del robot. | El formulario del creador, completo. El creador pone también lo que el robot no indica: tipo, estado, paso inicial y título (que puede ir vacío). |
| **Robot sin `Creador`** | Lo que envía. | Nada: JSON libre (como un caso sin formulario). |

**El caso recuerda su formulario** (`casos.formulario_id`): editar sus datos después se comprueba contra él, aunque el creador cambie
de formulario más tarde.

## Migración desde los tipos de caso

La migración `FormulariosDeCreador` no pierde nada:

1. Cada tipo de caso con esquema se convierte en un formulario del mismo proceso, **con el nombre y el id del tipo**.
2. Los creadores de ese tipo y los casos de ese tipo pasan a apuntar a ese formulario (así se siguen comprobando igual que antes).
3. Se quita la columna `esquema_datos_json` de los tipos.

El `Down` devuelve cada esquema a su tipo (por el id); un formulario creado después vuelve al tipo del primer creador que lo usa, si ese
tipo no tiene ya uno.

Lo único que cambia de comportamiento: un **robot que crea casos por tipo sin nombrar creador** deja de ver sus datos comprobados
contra el antiguo esquema del tipo. Para recuperarlo, que nombre el creador (`Creador = "Crear Bosch"`, SDK `Viriato.Rpa.Client`
0.10 / `Viariato.ApiContracts` 1.5).

## API

| | |
|---|---|
| `GET /api/v1/flujos/{flujoId}/formularios` (`flujos.read`) | Los formularios del proceso, con los creadores que usan cada uno. |
| `POST` / `PUT /…/formularios[/{id}]`, `DELETE /…/formularios/{id}` (permiso de creadores) | Crear, reemplazar entero y borrar. |
| Creadores (`/…/creadores`) | `formularioId` y `datosPorDefectoJson` nuevos en el cuerpo y en la respuesta. |
| `GET /api/v1/creadores-de-caso` | `esquemaDatosJson` es ahora el del formulario del creador, y trae `datosPorDefectoJson`. |
| `POST /api/v1/casos` | `formularioId` opcional. |

## Ampliar: opciones que salen de un parámetro del proceso (tarea 36 del backlog)

El formulario es el sitio donde encaja: una extensión del esquema en un campo `string`/`number`, p. ej.
`"x-opciones-del-parametro": "proveedores"`, que los dos validadores (`EsquemaDatos.cs` y `esquema.ts`) aceptan igual. Al guardar el
**formulario** se comprueba que el parámetro existe en su proceso y es una lista; al dibujarlo, una ruta da solo las listas que usa el
formulario a quien puede crear casos; y al crear el caso el servidor compara contra la lista **actual**. Como el formulario es del proceso
(igual que el parámetro), no hay que cruzar procesos. Los valores por defecto del creador también pasarían por esa comprobación.
