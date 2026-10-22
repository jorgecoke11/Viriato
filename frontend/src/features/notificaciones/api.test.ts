import { describe, expect, it } from 'vitest'
import { leerDestinatarios } from './api'

describe('leerDestinatarios', () => {
  it('acepta una por línea, comas o punto y coma, sin huecos ni repetidas', () => {
    expect(leerDestinatarios('a@b.c, d@e.f\n\n g@h.i; a@b.c ')).toEqual(['a@b.c', 'd@e.f', 'g@h.i'])
  })

  it('vacío no es ninguna dirección', () => {
    expect(leerDestinatarios('  \n , ')).toEqual([])
  })
})
