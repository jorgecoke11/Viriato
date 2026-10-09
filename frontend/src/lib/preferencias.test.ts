import { describe, expect, it } from 'vitest'
import { claveDePreferencia, guardarPreferencia, leerPreferencia, type Almacen } from './preferencias'
import { esRangoElegido, type RangoElegido } from './rangoDeFechas'

function almacenEnMemoria(): Almacen & { datos: Map<string, string> } {
  const datos = new Map<string, string>()
  return {
    datos,
    getItem: (c) => datos.get(c) ?? null,
    setItem: (c, v) => void datos.set(c, v),
    removeItem: (c) => void datos.delete(c),
  }
}

const POR_DEFECTO: RangoElegido = { tipo: 'atajo', atajo: 'hoy' }

describe('preferencias', () => {
  it('gives back what was saved', () => {
    const almacen = almacenEnMemoria()
    const elegido: RangoElegido = { tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-08' }

    guardarPreferencia('k', elegido, almacen)

    expect(leerPreferencia('k', POR_DEFECTO, esRangoElegido, almacen)).toEqual(elegido)
  })

  it('uses the initial value when nothing was saved', () => {
    expect(leerPreferencia('k', POR_DEFECTO, esRangoElegido, almacenEnMemoria())).toEqual(POR_DEFECTO)
  })

  it('ignores what it cannot read or does not understand — an old version of the page must not break the new one', () => {
    const almacen = almacenEnMemoria()
    almacen.datos.set('roto', '{no es json')
    almacen.datos.set('viejo', JSON.stringify({ tipo: 'hoy' }))

    expect(leerPreferencia('roto', POR_DEFECTO, esRangoElegido, almacen)).toEqual(POR_DEFECTO)
    expect(leerPreferencia('viejo', POR_DEFECTO, esRangoElegido, almacen)).toEqual(POR_DEFECTO)
  })

  it('works without any storage at all', () => {
    expect(() => guardarPreferencia('k', POR_DEFECTO, null)).not.toThrow()
    expect(leerPreferencia('k', POR_DEFECTO, esRangoElegido, null)).toEqual(POR_DEFECTO)
  })

  it('does not break when storage throws', () => {
    const explota: Almacen = {
      getItem: () => {
        throw new Error('bloqueado')
      },
      setItem: () => {
        throw new Error('lleno')
      },
      removeItem: () => {},
    }
    expect(() => guardarPreferencia('k', POR_DEFECTO, explota)).not.toThrow()
    expect(leerPreferencia('k', POR_DEFECTO, esRangoElegido, explota)).toEqual(POR_DEFECTO)
  })

  it('keeps one key per person', () => {
    expect(claveDePreferencia('ana', 'panel')).not.toBe(claveDePreferencia('luis', 'panel'))
    expect(claveDePreferencia(null, 'panel')).toBe('viriato:anonimo:panel')
  })
})
