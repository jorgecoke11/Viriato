import { describe, expect, it } from 'vitest'
import { CLASES_DE_CATEGORIA, CLASES_SIN_CATEGORIA, clasesDeCategoria, inicialDeCategoria } from './categorias'

describe('inicialDeCategoria', () => {
  it('es la primera letra en mayúscula', () => {
    expect(inicialDeCategoria('balay')).toBe('B')
    expect(inicialDeCategoria('  ñandú')).toBe('Ñ')
  })

  it('no falla con un nombre vacío', () => {
    expect(inicialDeCategoria('')).toBe('?')
  })
})

describe('clasesDeCategoria', () => {
  it('todos los tipos se dibujan igual, sin color propio: el color es de los estados', () => {
    expect(clasesDeCategoria('Balay')).toBe(clasesDeCategoria('Siemens'))
    expect(clasesDeCategoria('Balay')).toBe(CLASES_DE_CATEGORIA)
    for (const color of ['green', 'amber', 'red', 'blue', 'indigo', 'purple', 'teal', 'cyan', 'pink']) {
      expect(CLASES_DE_CATEGORIA.ficha).not.toContain(color)
    }
  })

  it('sin tipo es el mismo cuadrado, más discreto', () => {
    expect(clasesDeCategoria(null)).toBe(CLASES_SIN_CATEGORIA)
    expect(CLASES_SIN_CATEGORIA.ficha).not.toBe(CLASES_DE_CATEGORIA.ficha)
  })
})
