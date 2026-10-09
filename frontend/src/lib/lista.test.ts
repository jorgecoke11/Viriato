import { describe, expect, it } from 'vitest'
import { filtrosActivos, hayFiltros, paginarEnCliente, recogerIds } from './lista'

describe('filtrosActivos', () => {
  it('keeps only the filters that have a value', () => {
    expect(filtrosActivos({ estado: 'Fallido', proceso: '', texto: 'a' })).toEqual({ estado: 'Fallido', texto: 'a' })
    expect(filtrosActivos({})).toEqual({})
  })
})

describe('hayFiltros', () => {
  it('is false for an empty search and filters that are empty or at their default', () => {
    expect(hayFiltros('  ', { estado: '' })).toBe(false)
    expect(hayFiltros('', { situacion: 'activos' }, { situacion: 'activos' })).toBe(false)
  })

  it('is true for a search, or a filter that is not at its default', () => {
    expect(hayFiltros('placas', {})).toBe(true)
    expect(hayFiltros('', { estado: 'Fallido' })).toBe(true)
    expect(hayFiltros('', { situacion: '' }, { situacion: 'activos' })).toBe(true)
  })
})

describe('recogerIds', () => {
  const lista = Array.from({ length: 250 }, (_, i) => ({ id: `c${i}` }))
  const servidor = async (pagina: number, tamano: number) => ({ items: lista.slice((pagina - 1) * tamano, pagina * tamano) })

  it('reads the pages until it has all that match', async () => {
    const ids = await recogerIds(servidor, (f) => f.id, 250, 1000)
    expect(ids).toHaveLength(250)
    expect(ids[0]).toBe('c0')
    expect(ids[249]).toBe('c249')
  })

  it('stops at the maximum, with the first ones', async () => {
    const ids = await recogerIds(servidor, (f) => f.id, 250, 120)
    expect(ids).toHaveLength(120)
    expect(ids[119]).toBe('c119')
  })

  it('does not loop for ever when the server has fewer than it said', async () => {
    let llamadas = 0
    const ids = await recogerIds(
      async (pagina, tamano) => {
        llamadas++
        return { items: lista.slice((pagina - 1) * tamano, pagina * tamano).slice(0, pagina === 1 ? tamano : 0) }
      },
      (f) => f.id,
      250,
      1000,
    )
    expect(ids).toHaveLength(100)
    expect(llamadas).toBe(2)
  })

  it('asks for nothing when nothing matches', async () => {
    let llamadas = 0
    const ids = await recogerIds(async () => (llamadas++, { items: [] }), (f: { id: string }) => f.id, 0, 1000)
    expect(ids).toEqual([])
    expect(llamadas).toBe(0)
  })
})

describe('paginarEnCliente', () => {
  const plantillas = ['Prioridad Siemens', 'Turnos Balay', 'Orden por llegada', 'Extracción nocturna', 'Alta']
  const consulta = (busqueda: string, pagina: number, tamano: number) => ({ busqueda, filtros: {}, pagina, tamano })

  it('cuts the list into pages and says how many there are in all', () => {
    const primera = paginarEnCliente(plantillas, consulta('', 1, 2), (p) => p)
    const ultima = paginarEnCliente(plantillas, consulta('', 3, 2), (p) => p)
    expect(primera.items).toEqual(['Prioridad Siemens', 'Turnos Balay'])
    expect(primera.total).toBe(5)
    expect(ultima.items).toEqual(['Alta'])
  })

  it('searches by the text of each item, ignoring case and accents', () => {
    expect(paginarEnCliente(plantillas, consulta('extraccion', 1, 10), (p) => p).items).toEqual(['Extracción nocturna'])
    expect(paginarEnCliente(plantillas, consulta('BALAY', 1, 10), (p) => p).total).toBe(1)
    expect(paginarEnCliente(plantillas, consulta('zzz', 1, 10), (p) => p).items).toEqual([])
  })
})
