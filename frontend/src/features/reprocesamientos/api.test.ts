import { describe, expect, it } from 'vitest'
import { aSolicitud, aValores, describirCuando, describirEspera, VALORES_VACIOS, type ReglaDeReprocesamientoDto } from './api'

const regla: ReglaDeReprocesamientoDto = {
  id: 'r1',
  flujoId: 'f1',
  nombre: 'Portal caído',
  activa: true,
  orden: 1,
  estadoNegocioId: 'e1',
  estadoNegocioNombre: 'Portal caído',
  estadoEjecucion: 'Fallido',
  pasoNombre: 'Preparar',
  esperaMinutos: 30,
  limite: 4,
  estadoFinalId: 'e2',
  estadoFinalNombre: 'Revisar a mano',
  createdAt: '2026-10-22T10:00:00Z',
  updatedAt: '2026-10-22T10:00:00Z',
}

describe('aSolicitud', () => {
  it('sin marcar «poner límite» no hay límite ni estado final, aunque quedara uno escrito', () => {
    const solicitud = aSolicitud({ ...VALORES_VACIOS, nombre: ' Reintentar ', limite: '4', estadoFinalId: 'e2' })
    expect(solicitud).toMatchObject({ nombre: 'Reintentar', limite: null, estadoFinalId: null, estadoEjecucion: 'Fallido', pasoNombre: null })
  })

  it('vacío es «cualquiera» o «la ejecución en que acabó»', () => {
    const solicitud = aSolicitud({ ...VALORES_VACIOS, nombre: 'x', estadoEjecucion: '', estadoNegocioId: 'e1' })
    expect(solicitud.estadoEjecucion).toBeNull()
    expect(solicitud.estadoNegocioId).toBe('e1')
    expect(solicitud.pasoNombre).toBeNull()
  })

  it('ida y vuelta por el formulario deja la regla como estaba', () => {
    const { nombre, activa, orden, estadoNegocioId, estadoEjecucion, pasoNombre, esperaMinutos, limite, estadoFinalId } = regla
    expect(aSolicitud(aValores(regla))).toEqual({ nombre, activa, orden, estadoNegocioId, estadoEjecucion, pasoNombre, esperaMinutos, limite, estadoFinalId })
  })
})

describe('describir', () => {
  it('dice cuándo aplica con lo que tenga', () => {
    expect(describirCuando(regla)).toBe('Ejecución fallida · en «Portal caído»')
    expect(describirCuando({ ...regla, estadoEjecucion: null })).toBe('En «Portal caído»')
  })

  it('dice la espera en minutos y horas', () => {
    expect(describirEspera(0)).toBe('Al momento')
    expect(describirEspera(45)).toBe('45 min')
    expect(describirEspera(120)).toBe('2 h')
    expect(describirEspera(90)).toBe('1 h 30 min')
  })
})
