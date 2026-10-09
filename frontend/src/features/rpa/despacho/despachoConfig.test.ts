import { describe, expect, it } from 'vitest'
import { leerTope } from './despachoConfig'

describe('leerTope', () => {
  it.each([
    ['1', 1],
    ['3', 3],
    ['50', 50],
    [' 2 ', 2],
  ])('reads %j as the ceiling %d', (texto, esperado) => {
    expect(leerTope(texto)).toEqual({ valido: true, valor: esperado })
  })

  it.each(['', '   '])('reads an empty box (%j) as no ceiling, which is valid', (texto) => {
    expect(leerTope(texto)).toEqual({ valido: true, valor: null })
  })

  it.each(['0', '-1', '51', '1.5', 'dos', '1e1x', 'NaN'])('rejects %j', (texto) => {
    expect(leerTope(texto)).toEqual({ valido: false })
  })
})
