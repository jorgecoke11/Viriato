# Ideas de procesos para Viriato

Ideas de procesos para la vida diaria y para el trabajo. Ninguna está construida todavía, salvo las marcadas con ✅.

Todas se montan con las mismas piezas:

```
 lanzador (cron) o creador (formulario) ──▶ servicios por los que pasa el caso ──▶ robots que hacen cada servicio ──▶ almacén, documentos y avisos
```

- Lanzadores: [lanzadores-de-caso.md](lanzadores-de-caso.md)
- Formularios: [formularios.md](formularios.md)
- Tablas genéricas: [tablas-genericas.md](tablas-genericas.md)
- Almacén del proceso y notificaciones: [notificaciones.md](notificaciones.md)

Esfuerzo:
- 🟢 fácil: APIs públicas, sin webs difíciles.
- 🟡 medio: hay que leer webs o iniciar sesión.
- 🔴 difícil: captchas, certificados, bancos.

---

## 1. Lista de la compra inteligente 🟡

- **Qué es:** apuntas lo que necesitas y Viriato te dice en qué supermercado sale más barato.
- **Disparador:** un creador con formulario: la lista de productos (nombre y cantidad).
- **Servicios:** `Buscar precios` → `Proponer compra`.
- **Robots y fuentes:**
  - Un robot consulta 2 o 3 supermercados online (Mercadona, Eroski, Carrefour…) con el mismo enfoque HTTP + JSON-LD que el vigilante de precios.
  - Otro robot reparte la lista: todo en uno, o dividida si compensa.
- **Datos:** en el almacén, la equivalencia entre cada producto y su referencia en cada tienda, para no buscarlo cada vez. También el histórico de precios.
- **Avisos:** un correo con la propuesta y el total por tienda; la tabla va como documento.
- **Encaje:** existen formularios, almacén y notificaciones. Falta saber qué tiendas dejan leer sus precios sin bloquear: riesgo anti-bot, como con Amazon.
- **MVP:** una sola tienda y una lista corta, para comprobar que se pueden leer los precios.

## 2. Plantas, mascotas y mantenimiento de casa 🟢

- **Qué es:** recordatorios de lo que se olvida: regar, abonar, vacunas, desparasitar, filtros, la revisión de la caldera…
- **Disparador:** un lanzador semanal; su lista abre **un caso por tarea**.
- **Servicios:** `Comprobar vencimientos` → `Avisar` → `Hecho`. La persona marca el caso como hecho y así queda el historial.
- **Robots y fuentes:** un robot sencillo que mira la fecha de la última vez y la frecuencia de cada tarea.
- **Datos:** una tabla genérica de tareas: nombre, categoría, cada cuántos días y la última vez que se hizo.
- **Avisos:** una regla que manda correo cuando el caso llega al estado «Pendiente».
- **Encaje:** existe todo: lanzadores, tablas genéricas y reglas. Lo único dudoso es cómo actualiza el robot la «última vez» de la tabla al cerrar el caso.
- **MVP:** 5 tareas en la tabla, el lanzador de los lunes y un correo.

## 3. Recetas y menú semanal 🟢

- **Qué es:** cada domingo, el menú de la semana y su lista de la compra.
- **Disparador:** un lanzador `0 10 * * 0`, en `Europe/Madrid`.
- **Servicios:** `Proponer menú` → `Lista de la compra` → (opcional) pasar a la idea 1.
- **Robots y fuentes:** la IA local propone el menú según las preferencias (dieta, número de personas, lo que no gusta) y saca los ingredientes.
- **Datos:**
  - preferencias en los datos del lanzador;
  - menús anteriores en el almacén, para no repetir platos.
- **Avisos:** un correo con el menú y la lista.
- **Encaje:** existen los lanzadores y la IA local (la usa el trading). Puede encadenarse con la idea 1 para saber dónde comprar.
- **MVP:** menú y lista por correo, sin buscar precios.

## 4. Trading simulado ✅

Ya existe: 100 EUR simulados en un solo proceso que pasa por analizar → ejecutar → aprender ([trading-ia-local.md](trading-ia-local.md)). Ideas para ampliarlo:
- más estrategias, cada una como un dato del caso;
- comparar estrategias entre sí con el mismo capital;
- un informe semanal por correo con la evolución.

## 5. Vigilante de precios ✅

Ya existe: lee MediaMarkt y PcComponentes tres veces al día, guarda la evolución en el almacén y avisa con `precio_bajado`. Ideas para ampliarlo:
- más tiendas;
- alertas de Wallapop o Milanuncios por palabra clave y precio máximo;
- anuncios nuevos de Idealista o Fotocasa.

Amazon bloquea las peticiones que no vienen de un navegador. Su lanzador está en pausa (backlog 37).

## 6. Briefing matutino 🟢

- **Qué es:** cada día laborable, un solo correo con lo que necesitas antes de empezar.
- **Disparador:** un lanzador `30 7 * * 1-5` en `Europe/Madrid`; la ciudad, la latitud y la longitud van en el formulario del creador.
- **Servicios:** `Recopilar` → `Componer y avisar`.
- **Robots y fuentes:**
  - cada fuente es una pieza intercambiable (`IFuenteDeBriefing`);
  - el MVP tiene **el tiempo** (Open-Meteo, sin clave) y **la luz PVPC** (apidatos de REE, pública);
  - si una fuente falla, su sección dice «no disponible» y el caso no falla.
- **Datos:** el precio de la luz de cada día en el almacén (`luz:AAAA-MM-DD`), para compararlo con la media de la semana.
- **Avisos:**
  - el robot avisa con `briefing_listo`;
  - la regla «Briefing listo» manda el correo por la conexión Gmail, con asunto `Briefing {fecha}` y texto `{evento.texto}`;
  - una gráfica SVG de la luz se sube como evidencia.
- **Encaje:** existen lanzadores, reglas, almacén y la conexión de correo. El correo es solo texto plano (`ConexionDeCorreo.cs`, `TextPart("plain")`): para el MVP basta.
- **MVP:**
  - primero, tiempo y luz por correo;
  - después, noticias, calendario (URL ICS como credencial) y el estado de los robots de Viriato.

## 7. Resumen de newsletters / RSS / YouTube 🟢

- **Qué es:** un resumen semanal de lo que publican tus fuentes, para no leerlo todo.
- **Disparador:** un lanzador semanal; la lista de feeds va en sus datos.
- **Servicios:** `Recoger` → `Resumir` → `Enviar digest`.
- **Robots y fuentes:** feeds RSS o Atom (YouTube tiene RSS por canal) y, más adelante, un buzón de newsletters. La IA local resume cada elemento y agrupa por tema.
- **Datos:** en el almacén, lo ya enviado, para no repetir.
- **Avisos:** un correo con el digest; el texto completo va como documento.
- **Encaje:** existen los lanzadores, el almacén y la IA local. Para leer un buzón falta un tipo de conexión de **lectura de correo (IMAP)**.
- **MVP:** 5 feeds RSS con un resumen de una línea cada uno.

## 8. Stock y precios de la competencia 🟡

- **Qué es:** saber cada día a cuánto venden los rivales lo que vendes tú (pensado para Tecnoduero).
- **Disparador:** un lanzador diario con **un caso por SKU**. La lista de productos y sus URLs en cada rival va en el lanzador.
- **Servicios:** `Leer precios` → `Comparar` → `Sugerir precio`.
- **Robots y fuentes:** el mismo motor que el vigilante de precios, apuntado a las tiendas rivales.
- **Datos:**
  - histórico por SKU y rival en el almacén;
  - en los parámetros del proceso, el margen mínimo, para no sugerir vender a pérdidas.
- **Avisos:** un aviso solo cuando un rival baja de tu precio o se queda sin stock.
- **Encaje:** existe casi todo, porque es el vigilante con otro uso. Más adelante, aplicar el precio en la tienda (Medusa) sería otro servicio, con aprobación humana.
- **MVP:** 10 SKU, 2 rivales y un aviso por correo.

## 9. Salud y deporte 🟢

- **Qué es:** llevar el registro de entrenos, peso o medicación, con recordatorios y una gráfica mensual.
- **Disparador:**
  - un creador con formulario para cada registro: tipo, valor y nota;
  - un lanzador para los recordatorios, como la medicación diaria o el entreno de los martes y jueves;
  - otro lanzador mensual para el informe.
- **Servicios:** `Registrar`; en el proceso del informe, `Resumir mes` → `Avisar`.
- **Robots y fuentes:** un robot que agrupa los registros del mes y dibuja la gráfica SVG.
- **Datos:** los registros en una tabla genérica o en el almacén.
- **Avisos:** los recordatorios y el informe mensual por correo, con la gráfica como evidencia.
- **Encaje:** existen formularios, lanzadores, almacén y evidencias con imagen.
- **MVP:** registrar el peso y recibir una gráfica a fin de mes.

---

## Para el día a día

Pensadas para mi caso: vivo en Pamplona con mi pareja, que también usaría Viriato; tengo coche y teletrabajo.
- Lo que más tiempo me quita: la comida, las compras y organizarme.
- Me gusta: el gimnasio, aprender y leer, viajar y hacer planes, las series y películas, YouTube y los podcasts.
- Viriato **avisa y prepara** (el carrito, el borrador, la comparación), pero decido yo: no compra ni reserva solo.

Esquema de cada idea: *disparador → qué hace → qué te deja preparado*. ⭐ = recomendadas para empezar.

### Comida y compras

- **10 ⭐ Lista de la compra compartida.** Cualquiera de los dos apunta productos en un formulario desde el móvil y el caso de la semana los va acumulando. El viernes se prepara el carrito en el súper online y se avisa a los dos para revisarlo.
- **11 Menú en pareja con macros.** Amplía la idea 3: el menú se ajusta a la proteína y las calorías del gimnasio y a lo que no le gusta a cada uno. Puede ser comida de tupper para los días de teletrabajo.
- **12 Despensa y caducidades.** Registras lo que compras y cuándo caduca. Te avisa («caduca el jueves») y te propone una receta con lo que hay en casa.
- **13 Ofertas de tus productos habituales.** Vigila folletos y precios de tus 20 productos de siempre y avisa cada semana de cuáles están de oferta y dónde.
- **14 Batch cooking del domingo.** A partir del menú, un plan de cocinado ordenado (qué va primero y cuánto tarda) y qué tupper toca cada día.

### Tiempo y organización

- **15 ⭐ Semana en pareja.** El domingo a las 20:00 os llega a los dos un correo con la agenda de la semana, las tareas de casa, los planes y los cumpleaños cercanos.
- **16 Tareas de casa por turnos.** Un lanzador semanal reparte las tareas rotando y abre un caso por tarea y persona. Se marcan como hechas y a fin de mes sale quién ha hecho qué.
- **17 Apuntar rápido.** Un formulario de una línea («llamar al taller el martes») abre un caso que te lo recuerda a esa hora. Más adelante, también reenviando un correo (IMAP).
- **18 Cumpleaños y fechas importantes.** Aniversario y cumpleaños de la familia: aviso 2 semanas antes con ideas de regalo (IA local) dentro de un presupuesto.
- **19 Rutina de teletrabajo.** Cierre de jornada (qué hiciste y qué queda) y, los viernes, repaso semanal de objetivos.

### Gimnasio y salud

- **20 ⭐ Diario de entrenos y progresión.** Un formulario por sesión (ejercicio, series, kilos). Si llevas 3 sesiones estancado, propone subir la carga o hacer una descarga. Cada mes, una gráfica. Amplía la idea 9.
- **21 Hábitos diarios.** Un check-in por la noche (sueño, pasos, agua, proteína) que lleva la racha y manda un resumen semanal.
- **22 Precio de suplementos.** Proteína y creatina en el vigilante de precios (idea 5): solo hay que añadir los productos.

### Aprender y leer

- **23 Lista de lectura.** Tus libros pendientes, con aviso cuando bajan de precio en Kindle o están en la Biblioteca de Navarra.
- **24 Novedades de tu stack.** Releases de .NET, Next.js, Medusa y las librerías que usas (RSS de GitHub): un digest semanal con lo que te afecta.
- **25 Repaso espaciado.** Apuntas lo que aprendes y Viriato te lo pregunta por correo a los 1, 3, 7 y 30 días.

### Viajes y planes

- **26 ⭐ Planes del finde en Pamplona.** Cada jueves, la agenda cultural (Baluarte, el Ayuntamiento, conciertos) filtrada por vuestros gustos y con el tiempo previsto, para los dos.
- **27 Escapadas en puentes.** Cruza los festivos de Navarra con el precio de los vuelos desde Bilbao, Zaragoza y Madrid y el de casas rurales, y avisa con opciones ya preparadas.
- **28 Preparar un viaje.** Un formulario con destino y fechas abre un caso con varios servicios: checklist (DNI, seguro, reservas), previsión del tiempo, itinerario en borrador (IA) y recordatorio el día antes.
- **29 Restaurantes por probar.** Una lista compartida; cada mes os propone uno, con el enlace para reservar.

### Series, películas, YouTube y podcasts

- **30 ⭐ Watchlist compartida.** Apuntáis series y películas; avisa cuando llegan a vuestras plataformas o cuando sale una temporada nueva (TMDB / JustWatch).
- **31 Rotar suscripciones de streaming.** Mira la watchlist y te dice qué plataforma compensa este mes («cancela Netflix y activa Max»). Es ahorro, en línea con la meta FIRE.
- **32 Episodios nuevos con resumen.** Los canales y podcasts que sigues, con un resumen de la transcripción (IA local) para decidir si merece la pena verlos. Amplía la idea 7.

### Coche

- **33 ⭐ Coche al día.** Avisa con tiempo de la gasolina más barata cerca de casa (API pública del Ministerio), de la ITV, del seguro y de la revisión por kilómetros (los km se apuntan en un formulario). Cuando toca renovar el seguro, deja una comparación preparada.

---

## Siguientes pasos

- **Por dónde empezar:** el briefing matutino (6), que ya está analizado y solo usa piezas que existen.
- **Piezas genéricas que salen repetidas:**
  - la IA local como servicio común para cualquier proceso (3, 7);
  - nuevos tipos de conexión: IMAP para leer correo (7) y Telegram para avisos inmediatos;
  - correo en HTML, que daría avisos más legibles (6, 7, 8);
  - un motor de lectura de precios compartido entre el vigilante, la lista de la compra y la competencia (1, 5, 8, 13, 22, 23);
  - destinatarios por persona: que una regla avise a «los dos» o a quien le toca, en vez de escribir direcciones en cada regla (10, 15, 16, 26);
  - APIs públicas como fuentes reutilizables: gasolineras del Ministerio, TMDB, la agenda de Pamplona y los festivos de Navarra (26, 27, 30, 33);
  - checklist dentro del caso: pasos que una persona va marcando (16, 28).
