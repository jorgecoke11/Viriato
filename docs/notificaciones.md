# Notificaciones y conexiones: avisar cuando pasa algo en un caso

```
 algo pasa en un caso ──▶ queda en su historia ──▶ reglas del proceso que encajan ──▶ bandeja de salida ──▶ cartero ──▶ conexión ──▶ correo
 (un robot avisa,        (CasoEvento)              (mismo guardado: o todo o nada)      (EnvioDeNotificacion)  (cada 15 s,   (Gmail…)
  estado de negocio,                                                                                          reintenta)
  falla, termina)
```

## Conexiones: las cuentas de Viriato con el exterior

*Configuración general → Conexiones* (permiso `conexiones.manage`). Una conexión es una cuenta configurada una vez y usada por nombre
desde donde haga falta: hoy, las reglas de notificación. Su contraseña se guarda cifrada (la misma clave que las credenciales de los robots,
`Credenciales__ClaveCifrado`) y no se vuelve a mostrar. El botón del avión manda un mensaje de prueba.

**Tipos.** De momento, *Correo (SMTP)*. Cada tipo dice qué campos pide, y la pantalla los dibuja sola: añadir otro (Telegram, una API…) es
una clase que implementa `ITipoDeConexion` (y `ITipoQueEnviaMensajes` si envía mensajes) y una línea en
`backend/src/Viariato.Modules.Conexiones/DependencyInjection.cs`.

**Gmail.**
1. En tu cuenta de Google, activa la verificación en dos pasos.
2. Crea una *contraseña de aplicación* (Cuenta de Google → Seguridad → Contraseñas de aplicaciones). Son 16 letras.
3. En Viriato, una conexión de tipo *Correo* con:
   - servidor `smtp.gmail.com`;
   - puerto `587`;
   - usuario y remitente: tu dirección;
   - contraseña: esas 16 letras (la normal no vale).
4. Pruébala con el avión.

## Reglas: qué avisa, cuándo y a quién

*Procesos → el proceso → Notificaciones* (permiso `flujos.manage`). Cada regla:

| Campo | Qué es |
|---|---|
| Cuándo | **El robot avisa de algo** (con un código, p. ej. `precio_bajado`), **el caso llega a un estado de negocio**, **el caso falla** o **el caso termina**. |
| Tipo de caso | Solo los de ese tipo, o cualquiera. |
| Enviar por | Una conexión que envíe mensajes. |
| A quién | Direcciones de correo, una por línea. |
| Asunto y texto | Plantillas, como el título de un creador. |

En el asunto y el texto se pueden usar estas llaves:
- `{caso}`, `{proceso}`, `{tipo}`, `{estado}`, `{fecha}` y `{hora}` (en hora de Madrid);
- `{enlace}`, el caso en Viriato: sale de `PUBLIC_URL`, vía `Notificaciones__UrlPublica`;
- `{datos.campo}`, los datos del caso;
- `{evento.campo}`, lo que mandó el robot.

Una llave que no existe se rechaza al guardar. El botón del avión envía la regla ahora mismo, con el último caso del proceso, a la dirección
que digas.

Debajo, **Envíos**: lo que salió, lo pendiente y lo que falló, con el motivo.
- Un envío que falla se reintenta: al minuto, a los 5, a los 15 y a la hora.
- Tras 5 intentos queda *Fallido*, y se puede volver a mandar con «Reintentar».
- Cada envío deja una línea en la historia del caso (filtro *Avisos*).

**Un correo que no sale nunca bloquea ni falla un caso.** Los disparos solo escriben en la bandeja, y envía el cartero, aparte.

## Para quien escribe un robot

`Viriato.Rpa.Client` 0.9:

```csharp
await paso.IntentarNotificarAsync("precio_bajado", new { producto, antes, ahora, url }, ct); // no lanza nunca
```

El aviso queda en la historia del caso, y las reglas que escuchan ese código mandan sus mensajes. Sin reglas no se envía nada, y no es un
error. El robot no sabe de correos: qué se manda y a quién se cambia en Viriato.

**Almacén del proceso.** Memoria entre casos para los robots de un mismo proceso:

```csharp
var historial = await paso.ObtenerDatoAsync("precio:pccomponentes:abc", ct); // JsonElement? (null si no hay nada)
await paso.GuardarDatoAsync("precio:pccomponentes:abc", historial, ct);
```

- La clave son de 1 a 200 letras, números o `. _ : -`.
- El valor es cualquier JSON de hasta 64 KB.
- Un robot solo ve los datos de su proceso.

API: `GET`, `PUT` y `DELETE /api/v1/rpa/almacen/{clave}`.

**Imágenes.** Un archivo subido como evidencia con extensión de imagen (`grafico.png`, `grafico.svg`) se ve como imagen en la historia
del caso, aunque el robot no diga su tipo.

## El primero que lo usa

El robot *Vigilante de precios* (repositorio `vigilante_precios`):
- Lee el precio de un producto en MediaMarkt, PcComponentes o Amazon, a las 10:00, 16:00 y 20:00.
- Guarda su evolución en el almacén.
- Sube un gráfico y los datos.
- Avisa con `precio_bajado`. La regla «Precio bajado» de su proceso manda el correo.
