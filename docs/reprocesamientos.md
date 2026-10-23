# Reprocesamientos: volver a lanzar un caso que acabó mal

```
 el caso acaba ──▶ queda en su historia ──▶ primera regla del proceso ──▶ reprocesamiento programado ──▶ reprocesador ──▶ el paso vuelve a la cola
 (completado,      (CasoEvento)              que encaja (mismo guardado)    (casos.reprocesamientos)      (cada 10 s)       (con su prioridad)
  fallido, o                                         │
  cortado por                                        └──▶ límite agotado ──▶ el caso se queda en el estado final
  tiempo)
```

## Las reglas

*Procesos → el proceso → Reprocesamientos* (permiso `flujos.manage`). Cada regla:

| Campo | Qué es |
|---|---|
| Estado de negocio | El estado de negocio en que ha acabado el caso, o cualquiera. |
| Última ejecución | Cómo acabó la última ejecución: **fallida**, **completada** o **cancelada por exceso de tiempo**, o de cualquier forma. |
| Qué se reprocesa | Un paso del proceso, por nombre (aguanta versiones nuevas), o **la ejecución en que acabó el caso**. |
| Esperar | Minutos antes de reprocesar. 0 = al momento (en la siguiente vuelta del reprocesador, ≤10 s). |
| Poner límite | Sin marcar: sin límite. Marcada: cuántas veces como mucho se reprocesa un caso con esta regla. |
| Estado final | Solo con límite: el estado de negocio en que se queda el caso cuando se agotan. |
| Orden | Si varias reglas encajan, se aplica solo la de menor orden. |

Hace falta al menos uno de los dos estados. Si se ponen los dos, deben cumplirse los dos.

## Cuándo se mira

**Al acabar el caso**, nunca a mitad: cuando su ejecución termina completada, fallida o cancelada por exceso de tiempo. Se mira el estado de
negocio que tiene en ese momento y cómo acabó su último paso.

- **Un caso que una persona descarta no se reprocesa nunca** (estado de sistema `DESCARTADO`), aunque haya una regla de «cancelada».
- El contador es **por caso y por regla**: «4» son cuatro reprocesamientos de ese caso con esa regla.
- Al agotarse, el caso no se reprocesa más: sigue fallido (o como acabara), con el estado de negocio final. Ese cambio de estado es uno más:
  dispara las notificaciones que lo escuchen.
- Si cuando llega su momento el caso ya está otra vez en marcha (alguien le dio a «Reprocesar»), el reprocesamiento queda **Descartado** y
  no cuenta para el límite.
- Si el paso elegido no se puede relanzar (el caso no llegó a él), queda **Fallido** con el motivo, y en la historia del caso.
- Reprocesar un paso anterior vuelve a pasar por los que le siguen, cada uno como un intento nuevo.

## Dónde se ve

- Debajo de las reglas, **Reprocesamientos decididos**: pendientes, hechos, fallidos y descartados, con su caso.
- En la historia del caso, filtro *Reprocesos*: cada reprocesamiento («2 de 3») y el «Agotados».

## Cómo está hecho

- `ReglaDeReprocesamiento` y `ReprocesamientoProgramado` (tablas `casos.reglas_de_reprocesamiento` y `casos.reprocesamientos`).
- `DisparadorDeReprocesamientos`: un `SaveChangesInterceptor` que mira las líneas de historia que acaban un caso (`Completado`, `Fallido`,
  `Cancelado`), igual que las notificaciones. Decide en el mismo guardado que el final del caso: o todo o nada. Se registra antes que el de
  notificaciones para que el estado final también avise.
- `ReprocesadorDeCasos`: cada `Despacho:ReprocesamientosSegundos` (10) toma los que tocan con una actualización condicional (con varias
  réplicas solo uno lo ejecuta) y llama a `IEjecucionOrchestrator.ReprocesarPasoAsync`, lo mismo que el botón «Reprocesar».
- API: `GET/POST/PUT/DELETE /api/v1/flujos/{flujoId}/reprocesamientos`, `GET …/pasos` (nombres de los pasos de la versión activa) y
  `GET …/historial?estado=`.
