import { describe, expect, it } from 'vitest'
import { describirEvento, describirProgreso, progreso } from './api'

describe('progreso de una familia', () => {
  it('cuenta sobre los esperados mientras no han llegado todos', () => {
    expect(progreso({ miembros: 2, terminados: 1, participantesEsperados: 4 })).toBe(25)
    expect(describirProgreso({ miembros: 2, terminados: 1, participantesEsperados: 4, estado: 'Abierta' })).toBe('1 de 2 terminados · esperando 2 más')
  })

  it('sin esperados, cuenta sobre los que tiene', () => {
    expect(progreso({ miembros: 4, terminados: 3, participantesEsperados: null })).toBe(75)
    expect(describirProgreso({ miembros: 1, terminados: 0, participantesEsperados: null, estado: 'Cerrada' })).toBe('0 de 1 terminado')
  })

  it('vacía no divide entre cero', () => {
    expect(progreso({ miembros: 0, terminados: 0, participantesEsperados: null })).toBe(0)
  })
})

describe('historia de una familia', () => {
  it('dice cómo se cerró y cómo acabó', () => {
    expect(describirEvento({ accion: 'Cerrada', detalleJson: '{"miembros":3,"automatica":true}' })).toBe('Se cerró sola al llegar a 3 casos')
    expect(describirEvento({ accion: 'Terminada', detalleJson: '{"total":3,"completados":2,"fallidos":1,"cancelados":0}' })).toBe(
      'Terminada: 2 completados · 1 fallido · 0 cancelados',
    )
    expect(describirEvento({ accion: 'CierreNoAbierto', detalleJson: '{"motivo":"No todos acabaron bien."}' })).toBe('Sin caso de cierre: No todos acabaron bien.')
  })

  it('aguanta un detalle roto', () => {
    expect(describirEvento({ accion: 'Cerrada', detalleJson: 'no es json' })).toBe('Cerrada con 0 casos')
  })
})
