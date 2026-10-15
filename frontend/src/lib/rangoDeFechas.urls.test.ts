import { describe, expect, it } from 'vitest'
import { describirRango, esRangoElegido, finDelDia, inicioDelDia, instanteDesde, instanteHasta, rangoAParam, rangoDeParam, type RangoElegido } from './rangoDeFechas'

describe('en una URL', () => {
  it('writes a choice and reads it back', () => {
    const casos: RangoElegido[] = [
      { tipo: 'todos' },
      { tipo: 'atajo', atajo: '30d' },
      { tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-08' },
      { tipo: 'rango', desde: '2026-10-01' },
      { tipo: 'rango', hasta: '2026-10-08' },
      { tipo: 'rango', desde: '2026-10-08', hasta: '2026-10-08', horaDesde: '09:00', horaHasta: '18:30' },
      { tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-08', horaHasta: '12:15' },
    ]
    for (const caso of casos) expect(rangoDeParam(rangoAParam(caso))).toEqual(caso)
  })

  it('does not understand what is not ours', () => {
    for (const texto of [null, undefined, '', 'siempre', '2026-10-01', '2026-10-01_ayer', '_', '2026-02-30_2026-03-01', 'a_b_c', '2026-10-01T25:00_2026-10-02', '2026-10-01T9:00_2026-10-02', '_T09:00']) {
      expect(rangoDeParam(texto)).toBeNull()
    }
  })

  it("a day is bounded by its own first and last instant on the person's clock", () => {
    expect(new Date(inicioDelDia('2026-10-08')).getTime()).toBe(new Date(2026, 9, 8, 0, 0, 0, 0).getTime())
    expect(new Date(finDelDia('2026-10-08')).getTime()).toBe(new Date(2026, 9, 8, 23, 59, 59, 999).getTime())
  })
})

describe('con hora del día', () => {
  const dia: RangoElegido = { tipo: 'rango', desde: '2026-10-08', hasta: '2026-10-08' }

  it('a day on its own is its 24 hours', () => {
    expect(new Date(instanteDesde(dia)!).getTime()).toBe(new Date(2026, 9, 8, 0, 0, 0, 0).getTime())
    expect(new Date(instanteHasta(dia)!).getTime()).toBe(new Date(2026, 9, 8, 23, 59, 59, 999).getTime())
  })

  it('narrows the first and the last day to the times picked (the last minute included)', () => {
    const franja: RangoElegido = { ...dia, horaDesde: '09:00', horaHasta: '18:30' }
    expect(new Date(instanteDesde(franja)!).getTime()).toBe(new Date(2026, 9, 8, 9, 0, 0, 0).getTime())
    expect(new Date(instanteHasta(franja)!).getTime()).toBe(new Date(2026, 9, 8, 18, 30, 59, 999).getTime())
  })

  it('a shortcut and "todos" have no time of day', () => {
    expect(instanteHasta({ tipo: 'todos' })).toBeUndefined()
    expect(new Date(instanteDesde({ tipo: 'atajo', atajo: 'hoy' }, new Date(2026, 9, 8, 15))!).getHours()).toBe(0)
  })

  it('knows a choice with times is one of ours, but not times without their day', () => {
    expect(esRangoElegido({ tipo: 'rango', desde: '2026-10-08', hasta: '2026-10-08', horaDesde: '09:00' })).toBe(true)
    expect(esRangoElegido({ tipo: 'rango', hasta: '2026-10-08', horaDesde: '09:00' })).toBe(false)
    expect(esRangoElegido({ tipo: 'rango', desde: '2026-10-08', horaDesde: '9:00' })).toBe(false)
  })

  it('reads on a button with its times', () => {
    const ahora = new Date(2026, 9, 9)
    expect(describirRango({ ...dia, horaDesde: '09:00', horaHasta: '18:30' }, ahora)).toBe('8 oct, 09:00 – 18:30')
    expect(describirRango({ tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-08', horaDesde: '09:00' }, ahora)).toContain('09:00')
    expect(describirRango(dia, ahora)).toBe('8 oct')
  })
})
