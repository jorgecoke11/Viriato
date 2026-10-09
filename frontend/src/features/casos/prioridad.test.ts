import { describe, expect, it } from 'vitest'
import { describirPrioridad, etiquetaPrioridad, validarPrioridad } from './prioridad'

describe('validarPrioridad', () => {
  it.each(['0', '5', '-5', '1000', '-1000', ' 7 '])('accepts %s', (texto) => {
    expect(validarPrioridad(texto)).toBeNull()
  })

  it.each(['', '  ', '1.5', 'abc', '1e1x'])('rejects %j as not a whole number', (texto) => {
    expect(validarPrioridad(texto)).not.toBeNull()
  })

  it.each(['1001', '-1001', '99999'])('rejects %s as out of range, naming the limits', (texto) => {
    expect(validarPrioridad(texto)).toContain('-1000')
    expect(validarPrioridad(texto)).toContain('1000')
  })
})

describe('etiquetaPrioridad', () => {
  it('shows nothing for the ordinary priority', () => {
    expect(etiquetaPrioridad(0)).toBeNull()
  })

  it('signs the rest so higher and lower are told apart', () => {
    expect(etiquetaPrioridad(5)).toBe('Prioridad +5')
    expect(etiquetaPrioridad(-2)).toBe('Prioridad -2')
  })
})

describe('describirPrioridad', () => {
  it.each([
    [100, 'Urgente', 'alta'],
    [10, 'Alta', 'alta'],
    [0, 'Normal', 'normal'],
    [-10, 'Baja', 'baja'],
  ])('names the usual level %i as %s', (valor, texto, tono) => {
    expect(describirPrioridad(valor)).toEqual({ texto, tono })
  })

  it('shows any other value signed, with the tone of its side', () => {
    expect(describirPrioridad(7)).toEqual({ texto: '+7', tono: 'alta' })
    expect(describirPrioridad(-3)).toEqual({ texto: '-3', tono: 'baja' })
  })
})
