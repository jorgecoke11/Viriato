import { describe, expect, it } from 'vitest'
import type { LanzadorDelRobotDto } from '../api'
import { avisoDePendiente, lanzadorEnPantalla } from './lanzadorDelRobot'

const lanzador = (cambios: Partial<LanzadorDelRobotDto> = {}): LanzadorDelRobotDto => ({
  estado: 'SinTrabajo',
  copias: 0,
  replicas: 1,
  detalle: null,
  vistoEn: '2026-11-11T10:00:00Z',
  ...cambios,
})

describe('lanzadorEnPantalla', () => {
  it('says a robot with nothing to do will start by itself, without alarm', () => {
    expect(lanzadorEnPantalla(lanzador())).toEqual({ tono: 'neutral', texto: 'Sin trabajo: arrancará solo', detalle: null, enMarcha: false })
  })

  it('shows a running robot, and how many copies only when it may have several', () => {
    expect(lanzadorEnPantalla(lanzador({ estado: 'EnMarcha', copias: 1 })).texto).toBe('En marcha')
    expect(lanzadorEnPantalla(lanzador({ estado: 'EnMarcha', copias: 2, replicas: 3 }))).toMatchObject({
      tono: 'success',
      texto: '2 de 3 copias en marcha',
      enMarcha: true,
    })
  })

  it('keeps the reason a running robot does not run more copies', () => {
    const detalle = 'Esperando hueco: el lanzador ya tiene 2 de 2 robots en marcha.'
    expect(lanzadorEnPantalla(lanzador({ estado: 'EnMarcha', copias: 1, replicas: 2, detalle })).detalle).toBe(detalle)
  })

  it('warns when it waits and says why', () => {
    const detalle = 'Esperando la pantalla: ya hay 1 de 1 robots con navegador en marcha.'
    expect(lanzadorEnPantalla(lanzador({ estado: 'EnEspera', detalle }))).toEqual({ tono: 'warning', texto: 'Esperando para arrancar', detalle, enMarcha: false })
  })

  it('marks a robot that cannot be started as a problem, with what failed', () => {
    const visto = lanzadorEnPantalla(lanzador({ estado: 'Fallo', detalle: 'No se pudo arrancar: no existe "dotnet".' }))
    expect(visto.tono).toBe('danger')
    expect(visto.texto).toBe('No arranca')
    expect(visto.detalle).toContain('dotnet')
  })

  it('treats a blank reason as none and an unknown state as nothing to do', () => {
    expect(lanzadorEnPantalla(lanzador({ estado: 'Apagado', detalle: '   ' })).detalle).toBeNull()
    expect(lanzadorEnPantalla(lanzador({ estado: 'AlgoNuevo' })).texto).toBe('Sin trabajo: arrancará solo')
  })
})

describe('avisoDePendiente', () => {
  it('says nothing for a robot nobody launches', () => {
    expect(avisoDePendiente(null)).toBeNull()
    expect(avisoDePendiente(undefined)).toBeNull()
  })

  it('tells apart a robot being started, one waiting for room and one that fails', () => {
    expect(avisoDePendiente(lanzador())).toMatchObject({ tono: 'info', texto: 'Arrancando el robot' })
    expect(avisoDePendiente(lanzador({ estado: 'EnEspera', detalle: 'Esperando hueco.' }))).toEqual({
      tono: 'warning',
      texto: 'Esperando para arrancar',
      detalle: 'Esperando hueco.',
    })
    expect(avisoDePendiente(lanzador({ estado: 'Fallo' }))).toMatchObject({ tono: 'danger', texto: 'El robot no arranca' })
  })
})
