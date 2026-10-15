import { describe, expect, it } from 'vitest'
import { formatConSigno, formatEuros, formatPorcentaje } from './formato'

describe('formato de la cartera', () => {
  it('shows the sign of a change, always', () => {
    expect(formatConSigno(1.2, formatEuros)).toContain('+')
    expect(formatConSigno(-0.5, formatEuros)).toContain('−')
    expect(formatConSigno(0, formatEuros)).not.toMatch(/[+−]/)
  })

  it('writes a percentage with its sign', () => {
    expect(formatPorcentaje(9.98)).toBe('+9,98 %')
    expect(formatPorcentaje(-2)).toBe('−2 %')
    expect(formatPorcentaje(0)).toBe('0 %')
  })
})
