import { describe, expect, it } from 'vitest'
import { fechaLarga, primerNombre, saludoSegunHora } from './saludo'

const aLas = (hora: number) => new Date(2026, 9, 8, hora, 30)

describe('saludoSegunHora', () => {
  it.each([
    [6, 'Buenos días'],
    [13, 'Buenos días'],
    [14, 'Buenas tardes'],
    [20, 'Buenas tardes'],
    [21, 'Buenas noches'],
    [3, 'Buenas noches'],
    [0, 'Buenas noches'],
  ])('a las %s: %s', (hora, esperado) => {
    expect(saludoSegunHora(aLas(hora))).toBe(esperado)
  })
})

describe('fechaLarga', () => {
  it('dice el día de la semana y la fecha', () => {
    expect(fechaLarga(new Date(2026, 9, 8))).toBe('Jueves, 8 de octubre')
  })
})

describe('primerNombre', () => {
  it('es la primera palabra', () => {
    expect(primerNombre('Jorge López')).toBe('Jorge')
    expect(primerNombre('  Ana   María ')).toBe('Ana')
  })

  it('sin nombre, nada', () => {
    expect(primerNombre(undefined)).toBe('')
    expect(primerNombre('')).toBe('')
  })
})
