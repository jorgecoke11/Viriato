import { describe, expect, it } from 'vitest'
import { alternar, estadoDeSeleccion, marcarVarios } from './seleccion'

describe('alternar', () => {
  it('selects what was not selected and unselects what was, without touching the original', () => {
    const original = new Set(['a'])

    expect([...alternar(original, 'b')]).toEqual(['a', 'b'])
    expect([...alternar(original, 'a')]).toEqual([])
    expect([...original]).toEqual(['a'])
  })
})

describe('marcarVarios', () => {
  it('selects them all, keeping what was already selected elsewhere', () => {
    expect([...marcarVarios(new Set(['z']), ['a', 'b'], true)].sort()).toEqual(['a', 'b', 'z'])
  })

  it('unselects only the ones given', () => {
    expect([...marcarVarios(new Set(['a', 'b', 'z']), ['a', 'b'], false)]).toEqual(['z'])
  })

  it('does not mind ids that are not there or repeated', () => {
    expect([...marcarVarios(new Set(['a']), ['a', 'a', 'x'], true)].sort()).toEqual(['a', 'x'])
    expect([...marcarVarios(new Set(['a']), ['x'], false)]).toEqual(['a'])
  })
})

describe('estadoDeSeleccion', () => {
  it('says none, some or all of what is on screen is selected', () => {
    expect(estadoDeSeleccion(new Set(), ['a', 'b'])).toBe('ninguno')
    expect(estadoDeSeleccion(new Set(['a']), ['a', 'b'])).toBe('algunos')
    expect(estadoDeSeleccion(new Set(['a', 'b']), ['a', 'b'])).toBe('todos')
  })

  it('only looks at what is on screen: a selection kept from another page does not count', () => {
    expect(estadoDeSeleccion(new Set(['z']), ['a', 'b'])).toBe('ninguno')
    expect(estadoDeSeleccion(new Set(['z', 'a', 'b']), ['a', 'b'])).toBe('todos')
  })

  it('an empty page has nothing selected', () => {
    expect(estadoDeSeleccion(new Set(['a']), [])).toBe('ninguno')
  })
})
