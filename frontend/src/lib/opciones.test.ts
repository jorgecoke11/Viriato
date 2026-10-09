import { describe, expect, it } from 'vitest'
import { elegidosSegunExcluidos, excluidosTrasElegir, filtrarOpciones, marcarTodas, resumirSeleccion } from './opciones'

const procesos = [
  { id: '1', etiqueta: 'P01 - BSH Extracción de precios' },
  { id: '2', etiqueta: 'Proceso Alta' },
  { id: '3', etiqueta: 'Prueba de proceso' },
]

describe('filtrarOpciones', () => {
  it('finds by what the label contains, ignoring case and accents', () => {
    expect(filtrarOpciones(procesos, 'extraccion').map((o) => o.id)).toEqual(['1'])
    expect(filtrarOpciones(procesos, 'PROCESO').map((o) => o.id)).toEqual(['2', '3'])
  })

  it('returns everything for an empty search, and nothing when nothing matches', () => {
    expect(filtrarOpciones(procesos, '  ')).toHaveLength(3)
    expect(filtrarOpciones(procesos, 'zzz')).toEqual([])
  })
})

describe('resumirSeleccion', () => {
  it.each([
    [5, 5, 'Todos los procesos (5)'],
    [3, 100, '3 de 100 procesos'],
    [0, 4, 'Ningún proceso'],
    [0, 0, 'Sin procesos'],
  ])('%d of %d reads %j', (elegidas, total, esperado) => {
    expect(resumirSeleccion(elegidas, total, 'procesos', 'proceso')).toBe(esperado)
  })
})

describe('marcarTodas', () => {
  it('ticks or unticks only the ones given, and leaves the rest alone', () => {
    const base = new Set(['a', 'x'])
    expect([...marcarTodas(base, ['b', 'c'], true)].sort()).toEqual(['a', 'b', 'c', 'x'])
    expect([...marcarTodas(base, ['a', 'zz'], false)]).toEqual(['x'])
    expect([...base].sort()).toEqual(['a', 'x'])
  })
})

describe('elegidosSegunExcluidos / excluidosTrasElegir', () => {
  const ids = ['a', 'b', 'c']

  it('sin excluidos, está todo elegido', () => {
    expect([...elegidosSegunExcluidos(ids, [])]).toEqual(['a', 'b', 'c'])
  })

  it('lo excluido no está elegido, y lo nuevo sí aparece solo', () => {
    expect([...elegidosSegunExcluidos(['a', 'b', 'c', 'd'], ['b'])]).toEqual(['a', 'c', 'd'])
  })

  it('al elegir, se guarda lo que queda fuera', () => {
    expect(excluidosTrasElegir(ids, new Set(['a']), [])).toEqual(['b', 'c'])
    expect(excluidosTrasElegir(ids, new Set(ids), ['b'])).toEqual([])
  })

  it('conserva lo excluido que ya no está entre las opciones (por si vuelve)', () => {
    expect(excluidosTrasElegir(ids, new Set(['a', 'b']), ['zzz'])).toEqual(['zzz', 'c'])
  })
})
