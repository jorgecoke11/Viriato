import { describe, expect, it } from 'vitest'
import { leerMaximo } from './despachoConfig'

describe('leerMaximo', () => {
  it.each([
    ['1', 1],
    ['3', 3],
    ['50', 50],
    [' 2 ', 2],
  ])('reads %j as %d', (texto, esperado) => {
    expect(leerMaximo(texto)).toBe(esperado)
  })

  it.each(['', '   ', '0', '-1', '51', '1.5', 'dos', '1e1x', 'NaN'])('rejects %j', (texto) => {
    expect(leerMaximo(texto)).toBeNull()
  })
})
