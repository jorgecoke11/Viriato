# Trading con una IA local: planteamiento

> **Estado (10/10):** la **fase 0 está construida** y la **fase 1 probada de forma simple** (la IA local da una lectura como segunda opinión, sin decidir); la **cartera simulada ya vive en Viriato** y se ve en su pantalla «Cartera».
> Sobre la fase 0:
> **Estado:** la **fase 0 está construida** (ver el repositorio `trading_robots`, junto al de Viriato): datos, indicadores, reglas de riesgo y cartera simulada de 100 €, sin IA.
> Decisiones tomadas: valores de EE. UU. (grandes y ETF), capital simulado de 100 €, todo en pre, y dos capas: una **decisora** (análisis) y una **ejecutora** (órdenes), como dos procesos de Viriato.
> **No es asesoramiento financiero**: es el diseño de una herramienta de apoyo a la decisión. Las decisiones y el dinero son tuyos; nadie, ni una IA, predice con fiabilidad si una acción sube o baja.

## Lo que ya existe

El proceso **Trading** de pre tiene un caso por operación (tipo *Operación*: ticker, tipo, cantidad) y tres pasos:

| Paso | Tipo | Estados de negocio |
|---|---|---|
| Analizar operación | Interno | Pendiente de análisis → En análisis |
| Aprobar operación | Revisión humana | Pendiente de aprobación |
| Registrar operación | Interno | Operación registrada / Descartada |

La propuesta **no cambia ese esqueleto**: convierte «Analizar operación» en un análisis de verdad, hecho por un robot, y deja la aprobación humana donde está.

## Arquitectura

```
 Creador (lista de valores) ──▶ un caso por valor
                                     │
        ┌────────────────────────────▼─────────────────────────────┐
        │  Robot «Analista»  (.NET, con el SDK de Viriato, Docker) │
        │  1. Datos      precios OHLCV (Stooq / Yahoo)             │
        │  2. Indicadores RSI, MACD, medias 20/50/200, Bollinger,  │
        │                ATR, volumen, soportes   (cálculo normal) │
        │  3. IA local   Ollama ──▶ señal estructurada (JSON)      │
        │  4. Reglas     límites de riesgo (código, sin IA)        │
        └────────────────────────────┬─────────────────────────────┘
                                     ▼
              Caso: evidencias + estado «Pendiente de aprobación»
                                     ▼
                   Tú apruebas o descartas  (Revisión humana)
                                     ▼
              Registro: operación SIMULADA (cartera de papel)
                                     ▼
              A los N días: ¿acertó? ──▶ métricas de acierto
```

### 1. Los casos
- Un caso = un valor a analizar. Un **creador masivo** crea un caso por cada valor de una lista (parámetro `valores`, editable, igual que `productos` en P00).
- Lo que ya hace Viriato y se reutiliza: parámetros editables, barra de avance, evidencias, revisión humana, historial, permisos y el panel «En directo».
- **Falta un disparador diario** (hoy los casos se crean a mano con el creador). Opciones: un servicio programado que invoque al creador, o dejarlo manual al principio.

### 2. El robot «Analista»
Un robot más del tipo que ya tienes (.NET + SDK `Viriato.Rpa.Client`, en Docker). Todo en un solo lenguaje; no hace falta Python.
- **Datos:** velas diarias (y de 1 h si se quiere) de una fuente gratuita. Stooq (CSV) es la más estable; Yahoo no tiene API oficial y puede cambiar sin avisar;
  Alpha Vantage y Twelve Data tienen plan gratis con cupo pequeño. Se guarda lo descargado como evidencia del caso.
- **Indicadores:** se calculan con una librería (p. ej. *Skender.Stock.Indicators*, NuGet). Es matemática, no IA, y es reproducible.
- **Reglas de riesgo (código):** tamaño máximo por operación, exposición total, stop sugerido a partir de la ATR, y «no operar» si falta volumen o los datos son viejos.
  Estas reglas **pueden vetar** a la IA; la IA nunca puede saltárselas.

### 3. La IA local
- **Qué hace y qué no:** recibe los **indicadores ya calculados** (no los precios en bruto) y devuelve un JSON fijo:
  `{ señal: comprar | vender | mantener, confianza: 0-100, horizonte, razones[], riesgos[], stop_sugerido }`.
  Un modelo de lenguaje no es un oráculo: sirve para **sintetizar y explicar** lo que dicen los indicadores. La parte «predictiva» más honesta es un modelo clásico
  entrenado con histórico (p. ej. gradient boosting sobre los indicadores), medido con *backtesting*. La propuesta es empezar con indicadores + LLM y añadir ese
  modelo si los números lo justifican.
- **Cómo se ejecuta:** **Ollama** en un contenedor (perfil `ia` del compose, como el túnel), con API REST y salida forzada a JSON por esquema.
- **Modelo gratuito y su coste en este PC:** tiene unos **8 GB de RAM** y una gráfica AMD integrada (sin CUDA), así que correría **en CPU**. Candidatos de 3–7 mil millones
  de parámetros, cuantizados: *Qwen2.5-Instruct 3B/7B*, *Llama 3.2 3B*, *Phi-3.5-mini*, *Gemma 2 2B*. Un 7B ocupa ~4,7 GB (hay que subir el límite de memoria de Docker
  Desktop) y genera unos 2–5 tokens/s: ~1–2 min por valor. Vale para un lote nocturno de decenas de valores, no para tiempo real. En el PC de destino (otro) puede ser distinto: hay que mirar su RAM y si tiene GPU NVIDIA.

### 4. Aprobación y registro
- El paso **Aprobar operación** (ya existe) enseña la señal, las razones, los riesgos y las evidencias (gráfico con indicadores, JSON de la IA). Tú decides.
- **Registrar operación** apunta en una **cartera simulada** («de papel»): valor, precio, fecha, cantidad, stop. Sin dinero real.
- Un broker real (Alpaca, Interactive Brokers…) solo **después de meses de simulación con resultados medidos** y con tu autorización expresa, y aun así con aprobación humana en cada orden.

### 5. Medir si sirve
A los N días un paso comprueba el precio frente a la señal y guarda acierto/fallo y rentabilidad simulada. Con eso hay una tabla y un gráfico de **tasa de acierto, rentabilidad y
caída máxima** frente a «comprar y mantener». Si no supera esa referencia, no hay motivo para operar con dinero.

## Estado (10/10)
Implementado y probado en pre: fase 0, la IA local como segunda opinión (fase 1 simple), la cartera en Viriato, los **lanzadores de Viriato** (cron a las horas del mercado) y el **aprendizaje** (juzgar análisis, proponer pesos, reentrenar un modelo con validación). Todo en **un solo proceso («Trading simulado»)** con los servicios uno detrás de otro (analizar → ejecutar) y sin aprobación humana. Detalle en el `README.md` de `trading_robots`.
La IA local **no aprende**: solo comenta. Lo que se reentrena es un modelo pequeño (regresión logística, en CPU) y los pesos de las reglas; ninguno decide todavía: los pesos los aplica una persona y el modelo solo se mide.

## Fases
| Fase | Qué se hace | Cuándo se avanza |
|---|---|---|
| 0 | Datos + indicadores + reglas, sin IA; cartera simulada | cuando los datos y el cálculo sean fiables |
| 1 | IA local con salida JSON; explicación en el caso | cuando las señales sean coherentes con los indicadores |
| 2 | *Backtesting* y métricas de acierto | cuando haya meses de histórico |
| 3 | Simulación continua con aprobación humana | si supera a «comprar y mantener» |
| 4 | Broker real (opcional) | solo con tu decisión, tras la fase 3 |

## Riesgos que conviene tener presentes
- Los modelos pequeños **se equivocan con seguridad**: por eso las reglas de riesgo y la aprobación humana mandan.
- Los datos gratuitos tienen retraso, huecos y cambios; hay que validarlos antes de usarlos.
- Un buen resultado en el pasado (*backtesting*) no garantiza nada en el futuro; es fácil engañarse con el sobreajuste.
- Operar con dinero propio conlleva pérdidas posibles. Si algún día se hiciera para terceros, entra la regulación (CNMV).

## Decisiones que necesito de ti
1. ¿Qué mercados? (acciones de EE. UU., España, ETF…) y la lista inicial de valores.
2. ¿Qué horizonte? (diario, semanal) y cuánto capital se simularía.
3. ¿Qué PC lo correría? (RAM y, si hay, GPU NVIDIA): decide el tamaño del modelo.
4. ¿Empezamos por la fase 0 (sin IA) para fiarnos primero de los datos?
