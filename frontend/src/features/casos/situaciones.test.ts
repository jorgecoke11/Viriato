import { describe, expect, it } from 'vitest'
import type { TipoCasoConteoDto } from './api'
import { contarPorGrupo, GRUPOS, opcionesDeSituacion, valorDeGrupo } from './situaciones'

const tipo = (parcial: Partial<TipoCasoConteoDto>): TipoCasoConteoDto => ({
  tipoCasoId: null,
  nombre: 'T',
  orden: 0,
  enCurso: 0,
  finalizados: 0,
  porEstado: [],
  enEjecucion: 0,
  pendientes: 0,
  detenidos: 0,
  ...parcial,
})

describe('situaciones', () => {
  it('the four groups never share a technical estado', () => {
    const todos = Object.values(GRUPOS).flatMap((g) => g.estados)
    expect(new Set(todos).size).toBe(todos.length)
  })

  it('adds the four counts of many tipos', () => {
    const conteo = contarPorGrupo([tipo({ enEjecucion: 1, pendientes: 24, detenidos: 2, finalizados: 5 }), tipo({ enEjecucion: 3, finalizados: 1 })])
    expect(conteo).toEqual({ ejecutando: 4, pendiente: 24, detenido: 2, finalizado: 6 })
  })

  it('sends a group to the API as the list of its estados', () => {
    expect(valorDeGrupo('detenido')).toBe('Iniciado,Pausado,EsperandoRevisionHumana')
    expect(valorDeGrupo('ejecutando')).toBe('EnProgreso')
  })

  it('offers inside a group only its own estados, and every estado with no group', () => {
    expect(opcionesDeSituacion('finalizado').map((o) => o.valor)).toEqual(['', 'Completado', 'Fallido', 'Cancelado'])
    expect(opcionesDeSituacion(null)).toHaveLength(9)
  })
})
