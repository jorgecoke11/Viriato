import { apiFetch } from '../../lib/apiClient'

/** An open position of the simulated portfolio, valued at the last price the robots saw. */
export interface PosicionDto {
  ticker: string
  cantidad: number
  costeMedioUsd: number
  stopUsd: number | null
  precioActualUsd: number
  /** No price has been seen yet, so the position is valued at its cost. */
  precioEsElCoste: boolean
  precioDe: string | null
  valorEur: number
  gananciaEur: number
  gananciaPorcentaje: number
}

export interface MovimientoDto {
  id: string
  casoId: string
  ticker: string
  lado: 'Compra' | 'Venta'
  cantidad: number
  precioUsd: number
  eurUsd: number
  importeEur: number
  comisionEur: number
  efectivoTrasEur: number
  stopUsd: number | null
  fecha: string
}

export interface CarteraDto {
  capitalInicialEur: number
  efectivoEur: number
  valorPosicionesEur: number
  patrimonioEur: number
  rentabilidadEur: number
  rentabilidadPorcentaje: number
  eurUsd: number
  posiciones: PosicionDto[]
  movimientos: MovimientoDto[]
}

export const getCartera = () => apiFetch<CarteraDto>('/cartera')

export interface EstadisticaDto {
  senal: string
  muestras: number
  aciertos: number
  aciertoPorcentaje: number
  retornoMedioPorcentaje: number
}

export interface PropuestaDePesosDto {
  id: string
  pesos: Record<string, number>
  muestras: number
  motivo: string | null
  detalleJson: string | null
  creadaEn: string
}

export interface SenalRecienteDto {
  ticker: string
  fecha: string
  senal: string
  puntos: number
  senalIa: string | null
  confianzaIa: number | null
  ordenLado: string | null
  /** How the price moved after the days to wait; null until the signal is judged. */
  retornoPorcentaje: number | null
  acierto: boolean | null
  casoId: string
  probabilidadMl: number | null
}

export interface ModeloDto {
  id: string
  version: number
  estado: 'Activo' | 'Anterior' | 'Rechazado'
  muestrasDeEntrenamiento: number
  muestrasDePrueba: number
  precision: number
  precisionBase: number
  senales: number
  retornoMedioConSenalPorcentaje: number
  retornoMedioGeneralPorcentaje: number
  motivo: string | null
  creadoEn: string
}

export interface ModeloEnVivoDto {
  muestras: number
  aciertoPorcentaje: number
  senales: number
  retornoMedioConSenalPorcentaje: number
}

/** What the trading robots have learned so far. */
export interface AprendizajeDto {
  versionDePesos: number
  pesosActivos: Record<string, number>
  propuesta: PropuestaDePesosDto | null
  evaluadasTotal: number
  pendientesDeEvaluar: number
  estadisticas: EstadisticaDto[]
  recientes: SenalRecienteDto[]
  modeloActivo: ModeloDto | null
  modeloEnVivo: ModeloEnVivoDto | null
  modelos: ModeloDto[]
}

export const getAprendizaje = () => apiFetch<AprendizajeDto>('/cartera/aprendizaje')

/** Puts a proposed set of weights in force: the robots analyse with them from the next analysis. */
export const aplicarPropuesta = (id: string) => apiFetch<void>(`/cartera/aprendizaje/propuesta/${id}/aplicar`, { method: 'POST' })

export const descartarPropuesta = (id: string) => apiFetch<void>(`/cartera/aprendizaje/propuesta/${id}/descartar`, { method: 'POST' })
