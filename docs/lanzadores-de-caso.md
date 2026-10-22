# Lanzadores de caso (el cron de la plataforma)

Un **lanzador** es un creador de caso que se dispara solo. Se configura en el proceso (pestaña **Lanzadores**, junto a *Creadores*) y, cuando su cron dice, Viriato abre un caso exactamente como si una
persona hubiera elegido ese creador: con su tipo, el paso por el que empieza, su estado de negocio y su título; los datos del caso son los que se escriben en el lanzador. El caso no pertenece a ningún usuario.

Sirve para el trabajo que tiene que ocurrir a horas fijas («analizar la lista a las 10:00») sin necesidad de un robot que solo mire el reloj.

## Qué se configura

| Campo | Qué es |
|---|---|
| Creador | El creador de caso que se lanza (del mismo proceso). Decide qué caso se abre. |
| Cron | Expresiones cron de **cinco campos** (minuto hora día-del-mes mes día-de-la-semana), una por línea; se lanza cuando cualquiera coincide. `0 10 * * 1-5` = laborables a las 10:00. |
| Zona horaria | Nombre IANA (`America/New_York`, `Europe/Madrid`…). Las horas del cron se leen en ella, con el cambio de hora incluido. |
| Datos | La entrada del caso, escrita con el **formulario del creador** y empezando por sus **valores por defecto**; se guarda solo lo que cambia y, al lanzar, se mezcla campo a campo sobre los valores del creador ([formularios.md](formularios.md)). Se comprueba contra el formulario al guardar y al disparar. **Un objeto** («Un caso») abre un caso; **una lista** («Un caso por fila») abre **un caso por elemento** (hasta 200), cada uno con su título escrito a partir de sus datos: `[{"ticker":"AAPL"},{"ticker":"MSFT"}]` son dos casos. |
| Tolerancia | Hasta cuántos minutos tarde puede lanzarse un momento (1–1440, por defecto 120). |
| Activo | En pausa, no hace nada. |

## Cómo funciona

- Cada 30 s (`Despacho:LanzadoresSegundos`) un proceso de fondo de la API mira los lanzadores activos. Con varias réplicas de la API, solo una mira cada vez (cerrojo en la base de datos).
- Para cada lanzador calcula el **último momento del cron que ya llegó, no es más viejo que la tolerancia y viene después del último lanzado**. Si hay varios, se abre **un solo** caso (el del último).
- Lo lanzado se anota en el propio lanzador (el momento del cron, no la hora a la que se notó): un reinicio o una réplica nunca repiten un momento. Si la plataforma estuvo parada más que la tolerancia, ese momento se salta.
- Si no se puede abrir el caso (creador inactivo, proceso sin versión publicada, datos que ya no encajan, paso que ya no existe…), el motivo queda en el lanzador (columna «Último») y no se reintenta cada pocos segundos: se intenta de nuevo en el siguiente momento del cron.
- Al crear un lanzador, o al reactivarlo, **no se lanza lo que pasó antes**: empieza en el siguiente momento.

## Qué no hace (todavía)

- No conoce festivos ni calendarios de mercado: el cron es el cron.
- Un lanzador abre los casos de su lista en cada momento; si alguno no puede abrirse (por ejemplo, un elemento que ya no encaja con el formulario) se abren los demás y la columna «Último» dice cuántos y por qué.
- Si ningún robot atiende el servicio del paso inicial, los casos esperan en la cola.

API: `GET/POST /api/v1/flujos/{id}/lanzadores`, `PUT/DELETE …/lanzadores/{lanzadorId}` (permiso de creadores del proceso para escribir).
