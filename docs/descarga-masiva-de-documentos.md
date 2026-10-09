# Descargar los documentos de muchos casos en un zip

Una acción más del **Listado de casos** (y de cualquier lista con casillas): marca los casos —de uno en uno o «los N que
coinciden»— y pulsa **Descargar documentos de N casos**. El navegador recibe **un zip con una carpeta por caso**. No cambia nada en los
casos, así que no pide confirmación.

## Qué lleva el zip

- Lo mismo que la pestaña **Documentos** de cada caso: los que subió una persona y los archivos que generó un robot (un CSV, un PDF…).
  Las capturas y los vídeos **no** van: se ven en el historial del caso.
- Una carpeta por caso: `Título del caso (8 primeras letras del id)/`. El id evita que dos casos con el mismo título compartan carpeta.
- Si dos documentos de un caso se llaman igual, el segundo es `nombre (2).ext`. Los nombres se limpian (sin `/ \ : * ? " < > |`, sin
  `..`), así que nada puede salirse de la carpeta al descomprimir.
- Un **`LEEME.txt`** con lo que se descargó y, si hay, lo que se dejó fuera y por qué.

## Qué se deja fuera (y se avisa)

| Caso | Motivo |
|---|---|
| No existe, o es de un proceso al que no estás asignado | «Caso no encontrado.» (igual que en las demás acciones: no se confirma que exista) |
| No tiene documentos | «No tiene documentos.» |
| Tiene, pero el almacenamiento no los devuelve | Se dice en el `LEEME.txt`; no estropea el resto del zip. |

El aviso de la pantalla dice cuántos documentos y de cuántos casos, y por qué se dejó fuera cada uno. Si **ninguno** tiene documentos, no se
descarga nada (204) y el aviso lo explica.

## Límites

- **500 casos** por petición. Una selección mayor se descarga en **varios zips** seguidos, numerados (`…-parte-2-de-3.zip`); el navegador puede
  pedirte permiso para descargas múltiples la primera vez.
- Un zip admite hasta **5000 documentos** y **2 GB**; más, y el servidor responde 413 con el motivo: elige menos casos.
- El zip se arma en un archivo temporal del servidor (no en memoria) que se borra solo al terminar.

## Permiso

`casos.descargar` (el rol *User* lo trae) y, para ver la pantalla, `casos.masivas`. Está limitado por la asignación al proceso, como todo.

## API

`POST /api/v1/casos/documentos/zip` con `{ "ids": [...] }` → `200 application/zip` o `204`. El resumen viaja en la cabecera
`X-Viriato-Resumen` (JSON en base64: `documentos`, `casosConDocumentos`, `omitidosTotal` y los primeros 50 `omitidos` con su motivo).

## Código

Servidor: `Casos/Endpoints/DescargaMasivaDeDocumentosEndpoints.cs` (el zip) y `NombresParaZip.cs` (los nombres, con tests). Cliente: una
entrada en `features/casos/accionesMasivas.ts` (`descargar-documentos`); `resumenDeZip.ts` lee la cabecera. La acción es una entrada más del
registro de acciones masivas ([tablas-genericas.md](tablas-genericas.md)): el informe y el botón son los de siempre.
