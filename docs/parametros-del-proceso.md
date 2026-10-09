# Parámetros del proceso: cuáles puede cambiar el usuario desde el panel

Los parámetros (`iva`, `n_maximo_carrito`, `cupon`, la lista de productos…) son ajustes de un proceso que **los robots leen al empezar
cada caso**. Por defecto solo los gestiona quien administra el proceso. Algunos son una decisión de negocio, no técnica, y los puede
cambiar quien trabaja el proceso: se **abren** uno a uno.

## Abrir un parámetro

*Procesos → el proceso → Parámetros → editar*: marca **«Los usuarios pueden cambiarlo desde el panel»** y, si quieres, ponle un
**nombre para el panel** (`IVA (%)` en vez de `iva`). La lista muestra en la columna «En el panel» cuáles están abiertos. Desmarcarlo lo
cierra (y quita el nombre).

El flag no cambia nada de cómo lo lee el robot ni quién puede crear, renombrar o borrar parámetros: eso sigue siendo `flujos.manage`.
Un cliente que no conozca los campos nuevos al editar (solo manda valor y descripción) **no** los cierra por accidente.

## Cambiarlos

En el **Panel**, la tarjeta de cada proceso con parámetros abiertos tiene un botón *Cambiar parámetros de …* (junto al «+»). Abre una
ventana con un campo por parámetro —su nombre, su explicación y el valor; los valores largos o de varias líneas, en un cuadro de texto
con letra monoespaciada—, marca los modificados y guarda **todos a la vez o ninguno**. El cambio vale desde el siguiente caso que tome un
robot; no toca los que ya están en marcha.

Hace falta el permiso **`flujos.parametros`** y estar **asignado al proceso**. Si falta alguno de los dos, el servidor responde como si el
proceso no existiera. Un administrador no asignado tampoco lo ve.

## API

| | |
|---|---|
| `GET /api/v1/flujos/{flujoId}/parametros-editables` | Solo los abiertos, con su nombre (el código si no tienen). |
| `PUT /api/v1/flujos/{flujoId}/parametros-editables` | `{ valores: [{ id, valor }] }`. Todo o nada; 409 si alguno no existe o ya no está abierto; solo cambia el valor. |
| `GET /api/v1/casos/resumen` | Cada tarjeta trae `parametrosEditables`: el panel solo ofrece la acción si es mayor que 0. |

## Acciones de la tarjeta de un proceso

«Añadir caso» y «Cambiar parámetros» son entradas de una misma lista, `ACCIONES_DE_PROCESO`
(`frontend/src/features/casos/accionesDeProceso.tsx`): icono, etiqueta, permiso, cuándo aplica y la ventana que abren. La tarjeta dibuja las
que corresponden al usuario y al proceso. **Añadir otra acción es añadir una entrada**; la tarjeta no cambia.
