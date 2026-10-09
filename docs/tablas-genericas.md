# Tablas genéricas: búsqueda, filtros, páginas y acciones sobre todo lo que coincide

Toda lista que viene del servidor se muestra con las mismas piezas, así que todas se comportan igual: buscador y filtros arriba,
tabla con sus estados (cargando, error, vacío), páginas, y —si la lista tiene acciones— columna de selección, «seleccionar los N que
coinciden» (también más allá de la página en pantalla) y una barra con un botón por acción.

## Las piezas

| Pieza | Qué hace | Dónde |
|---|---|---|
| `useListaPaginada` | El **estado** de la lista: búsqueda (ya asentada antes de enviarla), filtros, página, selección (que sobrevive a cambiar de página o de filtro), «seleccionar todo lo que coincide», el informe de la última acción. No dibuja nada. | `frontend/src/lib/useListaPaginada.ts` |
| `ListaDeDatos` | **Dibuja** la lista: barra de búsqueda/filtros, tabla, estados, aviso de «seleccionar todos», páginas y barra de acciones. | `frontend/src/components/ui/ListaDeDatos.tsx` |
| `AvisoSeleccionarTodos`, `AccionesDeSeleccion`, `InformeDeAcciones` | Las partes de selección, sueltas, para quien necesite otra disposición (las usa también `CrudPage`). | `components/ui/AccionesDeLista.tsx` |
| `CrudPage` | Las pantallas de catálogo (servicios, equipos, usuarios, roles…): lo mismo, más crear/editar/borrar. Tiene paginación y acepta `acciones`. | `components/crud/CrudPage.tsx` |
| `lib/lista.ts` | La lógica pura (la pregunta al servidor, `recogerIds`), con tests. | `frontend/src/lib/lista.ts` |

## Añadir una tabla

1. **Columnas** como datos (`ColumnaDeTabla<T>`: clave, título, `celda`). Si varias pantallas listan lo mismo, ponlas en un hook
   (`useColumnasDeCasos`).
2. **El estado**: `useListaPaginada({ clave, cargar, obtenerId })`. `cargar` recibe `{ busqueda, filtros, pagina, tamano }` y llama a
   tu API (que debe aceptar `page` y `pageSize` y devolver `PagedResult`).
3. **La pantalla**: `<ListaDeDatos fuente={…} columnas={…} … />` con `buscador`, `filtros` (desplegable, texto o número) y, si
   quieres, `barraExtra` (por ejemplo un `SelectorDeRango` de fechas).

```tsx
const fuente = useListaPaginada<Trabajo>({ clave: ['ops-trabajos'], cargar, obtenerId: (t) => t.id })
<ListaDeDatos fuente={fuente} columnas={columnas} obtenerId={(t) => t.id} nombreDeFila={(t) => t.processCode}
  entidad={{ singular: 'trabajo', plural: 'trabajos' }} buscador={false}
  filtros={[{ clave: 'status', etiqueta: 'Estado', opciones }]} />
```

## Añadir una acción a una tabla

Una acción es un dato (`AccionMasiva`, `lib/accionesMasivas.ts`): etiqueta, icono, permiso, confirmación, qué ejecuta y qué listas
refresca. La lista de acciones de una pantalla es un array que se pasa a `acciones`:

- Sin acciones, la tabla **no tiene** columna de selección.
- Con acciones, aparece la selección y, al marcar algo, la barra con un botón por acción (la confirmación, el estado «en curso», el
  aviso y el informe de lo que se dejó sin hacer son los mismos para todas).
- Añadir otra acción es **añadir una entrada** al array. Las de casos están en `features/casos/accionesMasivas.ts` y en el servidor
  se registran con `IAccionMasivaSobreCaso` (ver [despacho-por-equipo.md](despacho-por-equipo.md), «Añadir una acción masiva»).
- Una acción puede ser una **descarga** (`descargar-documentos`: un zip con los documentos de los casos marcados, ver
  [descarga-masiva-de-documentos.md](descarga-masiva-de-documentos.md)): `ejecutar` entrega el archivo al navegador y devuelve el informe
  de siempre, con cifras propias (`datos`) y los omitidos sin detalle (`omitidosSinDetalle`).
- «Seleccionar los N que coinciden» recoge los ids de todo lo que cumple la búsqueda y los filtros actuales, hasta 1000
  (`maximoSeleccionable`); más allá, la lista dice cuántos coge y pide afinar el filtro.

## Dónde se usa

Listado de casos (que es también donde están las acciones masivas), la lista de casos de un grupo del panel (modal), Trabajos, Mercados,
Credenciales, Despliegues, Plantillas de despacho y todas las pantallas de `CrudPage`.

- Una lista que el servidor entrega entera y corta (Plantillas de despacho) se busca y se pagina en el cliente con
  `paginarEnCliente` (`lib/lista.ts`), para que se vea y se comporte como las demás.
- Una pantalla con filas que tienen sus propias acciones (editar, borrar, regenerar clave) las pone como una columna más.

## Lo que no pasa por aquí

Las tablas pequeñas que son **parte de un detalle**, no listas: las versiones de un flujo (`FlujoVersionesPanel`), y las que
cuelgan del detalle de una compañía o de un trabajo. Tienen pocas filas, siempre del mismo padre, y sus acciones son del padre.

## API: lo que tiene que aceptar un listado

`GET …?search=…&page=1&pageSize=25` y la respuesta `{ items, page, pageSize, total }`. Los listados de catálogo ya pasan `page` y
`pageSize` (antes algunos fijaban 100 e ignoraban `page`, así que más de 100 elementos no se veían).
