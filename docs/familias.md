# Familias: casos que acaban juntos

```
 robot / persona            casos que se crean              terminador de familias (cada 10 s)
 crea la familia ──▶ entran en ella al nacer ──▶ se cierra ──▶ todos han acabado ──▶ Terminada ──▶ caso de cierre
 (tipo, nombre,       (o se añaden después)       (a mano, por    (y ninguno espera        (resumen     └─▶ avisos «Familia
  esperados)                                       el robot o sola  un reprocesamiento)      fijado)          terminada»
                                                   al llegar a N)
```

Una **familia** agrupa casos de un proceso que se ven y acaban juntos (p. ej. «Extracción BSH 18/11»: un caso por marca y producto).
Cuando todos sus casos han terminado, la familia termina y hace lo que diga su tipo.

## Tipos de familia

*Procesos → el proceso → Familias* (permiso `flujos.manage`). Cada tipo:

| Campo | Qué es |
|---|---|
| Nombre | Único en el proceso. Los robots lo usan al crear una familia. |
| Al terminar, abrir un caso con | Un creador de casos (de este proceso o de otro). Vacío: no se abre caso de cierre. |
| Abrirlo | **Siempre**, o **solo si todos acabaron completados**. |
| Activo | Un tipo inactivo no admite familias nuevas; las que tiene siguen su curso. Un tipo con familias no se borra. |

## La vida de una familia

| Estado | Qué pasa |
|---|---|
| **Abierta** | Le entran casos. |
| **Cerrada** | No entran más. Termina en cuanto acaban todos los que tiene. |
| **Terminada** | Sus cifras (completados, fallidos, cancelados) quedan fijas y se han hecho sus acciones. |

- **Cerrar**: a mano (botón en la familia), el robot (`CerrarFamiliaAsync`) o **sola**, si al crearla se dijeron sus
  **participantes esperados** y ya los tiene. Con esperados, no admite más de esos. Sin esperados y sin cerrarla, **nunca termina**.
- **Un caso ha terminado** cuando está completado, fallido o cancelado **y no tiene un reprocesamiento pendiente**: un caso fallido que
  una regla va a relanzar no cuenta como terminado hasta que acabe de verdad.
- Un caso puede estar en **varias familias**. Una familia cerrada con cero casos termina enseguida.

## Lo que hace al terminar

- **Caso de cierre**: si el tipo tiene creador (y se cumple la condición), se abre un caso con él. Recibe en sus datos:

  ```json
  { "familia": { "id": "…", "nombre": "Extracción 18/11", "tipo": "Extracción del día",
                 "total": 3, "completados": 2, "fallidos": 1, "cancelados": 0,
                 "casos": [ { "id": "…", "titulo": "Lavadoras Balay", "estado": "Completado", "estadoNegocio": "PRECIOS_EXTRAIDOS" } ] } }
  ```

  Los **datos** de cada caso no van ahí (una familia grande haría un caso enorme): el robot del caso de cierre los pide con
  `ObtenerFamiliaAsync(familia.id)`. Si el caso de cierre no se puede abrir (el creador está inactivo, sus datos no encajan con su
  formulario…), la familia termina igual y lo dice en su historia y en «Nota de cierre». No se reintenta.
- **Avisos**: en *Notificaciones* del proceso, disparador **«Una familia termina»**, de un tipo de familia o de cualquiera. En el aviso,
  `{caso}` es el nombre de la familia, `{tipo}` su tipo, `{datos.total}`, `{datos.completados}`, `{datos.fallidos}` y
  `{datos.cancelados}` cómo acabaron sus casos, y `{enlace}` abre la familia. Lo enviado queda en la historia de la familia.

## Para quien escribe un robot

`Viriato.Rpa.Client` 0.13 (contratos 1.8.0). El despliegue necesita **Puede crear casos en**: la familia es de ese proceso.

```csharp
var familia = await paso.CrearFamiliaAsync(new CrearFamiliaRobotRequest("Extracción del día", $"Extracción {hoy:dd/MM}", ParticipantesEsperados: casos.Count));
foreach (var datos in casos)
    await paso.CrearCasoAsync(new CrearCasoRobotRequest(null, datos, Creador: "Extracción", Familias: [familia.Id]));
// Con ParticipantesEsperados no hace falta: se cierra sola al llegar al último.
await paso.CerrarFamiliaAsync(familia.Id);
```

Y el robot del caso de cierre:

```csharp
var id = JsonDocument.Parse(paso.Ejecucion.DatosCasoJson!).RootElement.GetProperty("familia").GetProperty("id").GetGuid();
var familia = await paso.ObtenerFamiliaAsync(id); // familia.Casos[i].DatosJson
```

Un robot ve las familias de su proceso, las del proceso en que crea casos y aquellas cuyo caso de cierre es de su proceso.

API de robots: `POST /api/v1/rpa/familias`, `POST /api/v1/rpa/familias/{id}/cerrar`, `GET /api/v1/rpa/familias/{id}`, y `familias` en
`POST /api/v1/rpa/casos`.

## Para personas

- **Monitorización → Familias**: todas las de tus procesos, con su avance («3 de 5 terminados · esperando 2 más»). Cada una muestra sus
  casos (el listado de siempre), su caso de cierre y su historia.
- En la ficha de un caso, bajo el título, sus familias.
- API (`casos.read` para ver, `casos.crear` para crear, cerrar y añadir): `GET/POST /api/v1/familias`, `GET /api/v1/familias/{id}`,
  `GET /api/v1/familias/{id}/eventos`, `POST /api/v1/familias/{id}/cerrar`, `POST /api/v1/familias/{id}/casos`,
  `GET /api/v1/casos/{id}/familias` y `GET /api/v1/casos?familiaId=`. Acción masiva `anadir-a-familia` con `{ "familiaId": "…" }`
  (todavía sin botón en el listado).

## Cómo está hecho

- `GestorDeFamilias` crea, llena y cierra (mismas reglas para personas y robots). Terminar **no** es suyo.
- `TerminadorDeFamilias` (cada `Despacho:FamiliasSegundos`, 10 s): en una transacción con un cerrojo de Postgres, como los lanzadores, así
  que con varias réplicas de la API solo una termina familias y sus acciones ocurren una vez. Primero cierra las abiertas que llegaron a sus
  esperados; luego termina hasta 50 cerradas sin casos pendientes. Es un worker y no una reacción al final de cada caso: da igual quién o
  cómo acabara el último caso, y una vuelta perdida (la API caída) se recupera en la siguiente.
- Solo mira las familias abiertas y cerradas: el índice sobre el estado es parcial (`estado <> 'Terminada'`), así que las terminadas, que
  crecen siempre, no lo encarecen.
- El aviso sale como todos: el terminador escribe «Terminada» en la historia de la familia (`familia_eventos`) y el interceptor de
  notificaciones encola los avisos en ese mismo guardado.
- Tablas (esquema `casos`): `tipos_de_familia`, `familias`, `familia_miembros`, `familia_eventos`. Los envíos (`notificaciones.envios`)
  tienen `familia_id` y su `caso_id` puede ser nulo.
