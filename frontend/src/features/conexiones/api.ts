import { apiFetch } from '../../lib/apiClient'

export interface CampoDeConexionDto {
  codigo: string
  etiqueta: string
  tipo: 'texto' | 'numero' | 'booleano'
  obligatorio: boolean
  porDefecto: string | number | boolean | null
  ayuda: string | null
}

export interface TipoDeConexionDto {
  codigo: string
  nombre: string
  etiquetaSecreto: string | null
  enviaMensajes: boolean
  campos: CampoDeConexionDto[]
}

export interface ConexionDto {
  id: string
  nombre: string
  tipo: string
  tipoNombre: string
  configuracion: Record<string, string | number | boolean>
  tieneSecreto: boolean
  enviaMensajes: boolean
  activa: boolean
  createdAt: string
  updatedAt: string
}

export interface GuardarConexionRequest {
  nombre: string
  tipo: string
  configuracion: Record<string, string | number | boolean>
  /** Only when it changes: empty keeps the stored one (it never comes back). */
  secreto: string | null
  activa: boolean
}

export interface ConexionParaEnviarDto {
  id: string
  nombre: string
  tipo: string
  tipoNombre: string
}

export interface ResultadoDePrueba {
  enviado: boolean
  destinatario?: string
  error?: string
}

export const listTiposDeConexion = () => apiFetch<TipoDeConexionDto[]>('/conexiones/tipos')
export const listConexiones = () => apiFetch<ConexionDto[]>('/conexiones')
/** The accounts that can send messages, for whoever configures a process's notifications (names only). */
export const listConexionesParaEnviar = () => apiFetch<ConexionParaEnviarDto[]>('/conexiones/para-enviar')
export const createConexion = (input: GuardarConexionRequest) => apiFetch<ConexionDto>('/conexiones', { method: 'POST', body: JSON.stringify(input) })
export const updateConexion = (id: string, input: GuardarConexionRequest) =>
  apiFetch<ConexionDto>(`/conexiones/${id}`, { method: 'PUT', body: JSON.stringify(input) })
export const deleteConexion = (id: string) => apiFetch<void>(`/conexiones/${id}`, { method: 'DELETE' })
export const probarConexion = (id: string, destinatario: string | null) =>
  apiFetch<ResultadoDePrueba>(`/conexiones/${id}/probar`, { method: 'POST', body: JSON.stringify({ destinatario }) })
