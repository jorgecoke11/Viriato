import { describe, expect, it } from 'vitest'
import {
  aDia,
  compararDias,
  deDia,
  describirDias,
  describirRango,
  esDia,
  esRangoElegido,
  ordenarDias,
  resolverAtajo,
  resolverRango,
  semanasDelMes,
  sumarDias,
} from './rangoDeFechas'

// Thursday the 8th of October, 2026, in the afternoon.
const AHORA = new Date(2026, 9, 8, 15, 30)

describe('días', () => {
  it('goes to a day and back without sliding by the time zone', () => {
    expect(aDia(deDia('2026-10-08'))).toBe('2026-10-08')
    expect(deDia('2026-10-08').getHours()).toBe(0)
  })

  it('adds days across months and years', () => {
    expect(sumarDias('2026-10-31', 1)).toBe('2026-11-01')
    expect(sumarDias('2026-01-01', -1)).toBe('2025-12-31')
    expect(sumarDias('2026-03-01', -1)).toBe('2026-02-28')
  })

  it('orders two days whichever way round they come', () => {
    expect(ordenarDias('2026-10-09', '2026-10-02')).toEqual({ desde: '2026-10-02', hasta: '2026-10-09' })
    expect(compararDias('2026-10-02', '2026-10-09')).toBe(-1)
  })

  it('only accepts real days', () => {
    expect(esDia('2026-10-08')).toBe(true)
    expect(esDia('2026-02-30')).toBe(false)
    expect(esDia('08/10/2026')).toBe(false)
    expect(esDia(20261008)).toBe(false)
  })
})

describe('resolverAtajo', () => {
  it.each([
    ['hoy', '2026-10-08', '2026-10-08'],
    ['ayer', '2026-10-07', '2026-10-07'],
    ['7d', '2026-10-02', '2026-10-08'],
    ['30d', '2026-09-09', '2026-10-08'],
    ['mes', '2026-10-01', '2026-10-08'],
    ['mesPasado', '2026-09-01', '2026-09-30'],
  ] as const)('%s is %s to %s', (atajo, desde, hasta) => {
    expect(resolverAtajo(atajo, AHORA)).toEqual({ desde, hasta })
  })

  it('last month works across a year boundary', () => {
    expect(resolverAtajo('mesPasado', new Date(2026, 0, 15))).toEqual({ desde: '2025-12-01', hasta: '2025-12-31' })
  })
})

describe('resolverRango', () => {
  it('"todos" is no days at all, a range is its own days, an open end stays open', () => {
    expect(resolverRango({ tipo: 'todos' }, AHORA)).toEqual({})
    expect(resolverRango({ tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-03' }, AHORA)).toEqual({ desde: '2026-10-01', hasta: '2026-10-03' })
    expect(resolverRango({ tipo: 'rango', desde: '2026-10-01' }, AHORA)).toEqual({ desde: '2026-10-01', hasta: undefined })
  })

  it('a shortcut follows the day: the same choice means something else tomorrow', () => {
    const ayer = resolverRango({ tipo: 'atajo', atajo: 'hoy' }, new Date(2026, 9, 7))
    const hoy = resolverRango({ tipo: 'atajo', atajo: 'hoy' }, AHORA)
    expect(ayer.desde).toBe('2026-10-07')
    expect(hoy.desde).toBe('2026-10-08')
  })
})

describe('esRangoElegido', () => {
  it('recognises what it saves and rejects what it does not understand', () => {
    expect(esRangoElegido({ tipo: 'todos' })).toBe(true)
    expect(esRangoElegido({ tipo: 'atajo', atajo: '7d' })).toBe(true)
    expect(esRangoElegido({ tipo: 'rango', desde: '2026-10-01' })).toBe(true)
    expect(esRangoElegido({ tipo: 'atajo', atajo: 'siempre' })).toBe(false)
    expect(esRangoElegido({ tipo: 'rango', desde: 'ayer' })).toBe(false)
    expect(esRangoElegido({ tipo: 'hoy' })).toBe(false)
    expect(esRangoElegido(null)).toBe(false)
    expect(esRangoElegido('hoy')).toBe(false)
  })
})

describe('describir', () => {
  it('names a shortcut and the "no limit" choice', () => {
    expect(describirRango({ tipo: 'atajo', atajo: '7d' }, AHORA)).toBe('Últimos 7 días')
    expect(describirRango({ tipo: 'todos' }, AHORA, 'Todos los finalizados')).toBe('Todos los finalizados')
  })

  it('writes a range short, with the year only when it matters', () => {
    expect(describirDias('2026-10-08', '2026-10-08', AHORA)).toBe('8 oct')
    expect(describirDias('2026-10-01', '2026-10-08', AHORA)).toBe('1 oct – 8 oct')
    expect(describirDias('2025-12-28', '2026-01-03', AHORA)).toBe('28 dic 2025 – 3 ene 2026')
    expect(describirDias('2025-10-08', '2025-10-09', AHORA)).toBe('8 oct – 9 oct 2025')
    expect(describirDias('2026-10-01', undefined, AHORA)).toBe('Desde el 1 oct')
  })
})

describe('semanasDelMes', () => {
  it('has six weeks of seven days, Monday first, and marks the days of the month', () => {
    const semanas = semanasDelMes(2026, 9) // October 2026 starts on a Thursday
    expect(semanas).toHaveLength(6)
    expect(semanas.every((s) => s.length === 7)).toBe(true)
    expect(semanas[0][0].dia).toBe('2026-09-28') // the Monday before
    expect(semanas[0][3]).toEqual({ dia: '2026-10-01', delMes: true })
    expect(semanas[0][2].delMes).toBe(false)
    expect(semanas.flat().filter((d) => d.delMes)).toHaveLength(31)
  })

  it('a month that starts on a Monday has no leading days', () => {
    const semanas = semanasDelMes(2026, 5) // June 2026 starts on a Monday
    expect(semanas[0][0]).toEqual({ dia: '2026-06-01', delMes: true })
  })
})
