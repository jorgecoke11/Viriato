import { describe, expect, it } from 'vitest'
import { finDelDia, inicioDelDia, rangoAParam, rangoDeParam, type RangoElegido } from './rangoDeFechas'

describe('en una URL', () => {
  it('writes a choice and reads it back', () => {
    const casos: RangoElegido[] = [
      { tipo: 'todos' },
      { tipo: 'atajo', atajo: '30d' },
      { tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-08' },
      { tipo: 'rango', desde: '2026-10-01' },
      { tipo: 'rango', hasta: '2026-10-08' },
    ]
    for (const caso of casos) expect(rangoDeParam(rangoAParam(caso))).toEqual(caso)
  })

  it('does not understand what is not ours', () => {
    for (const texto of [null, undefined, '', 'siempre', '2026-10-01', '2026-10-01_ayer', '_', '2026-02-30_2026-03-01', 'a_b_c']) {
      expect(rangoDeParam(texto)).toBeNull()
    }
  })

  it("a day is bounded by its own first and last instant on the person's clock", () => {
    expect(new Date(inicioDelDia('2026-10-08')).getTime()).toBe(new Date(2026, 9, 8, 0, 0, 0, 0).getTime())
    expect(new Date(finDelDia('2026-10-08')).getTime()).toBe(new Date(2026, 9, 8, 23, 59, 59, 999).getTime())
  })
})
