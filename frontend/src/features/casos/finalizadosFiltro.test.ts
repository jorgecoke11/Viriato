import { describe, expect, it } from 'vitest'
import { filtroToListParams, filtroToParams } from './finalizadosFiltro'
import { duracion, formatFecha, haceCuanto, ultimoResultado } from './fechas'

// Local midnight of a given day, built without going through a string, to compare against what the code sends.
const local = (y: number, m: number, d: number, h = 0, min = 0, s = 0, ms = 0) => new Date(y, m - 1, d, h, min, s, ms).getTime()

describe('filtroToParams', () => {
  it('"hoy" is the user\'s own day, from its first to its last instant, not UTC', () => {
    const ahora = new Date(2026, 9, 8, 0, 30) // 00:30 local: still the 8th for the user, whatever UTC says
    const { desde, hasta, finalizados } = filtroToParams({ tipo: 'atajo', atajo: 'hoy' }, ahora)

    expect(new Date(desde!).getTime()).toBe(local(2026, 10, 8))
    expect(new Date(hasta!).getTime()).toBe(local(2026, 10, 8, 23, 59, 59, 999))
    expect(finalizados).toBeUndefined()
  })

  it('a shortcut covers the days it names', () => {
    const ahora = new Date(2026, 9, 8, 15, 0)
    const { desde, hasta } = filtroToParams({ tipo: 'atajo', atajo: '7d' }, ahora)

    expect(new Date(desde!).getTime()).toBe(local(2026, 10, 2))
    expect(new Date(hasta!).getTime()).toBe(local(2026, 10, 8, 23, 59, 59, 999))
  })

  it('"todos" asks for no limit', () => {
    expect(filtroToParams({ tipo: 'todos' })).toEqual({ finalizados: 'todos' })
  })

  it('a range covers its first and last day entirely', () => {
    const { desde, hasta } = filtroToParams({ tipo: 'rango', desde: '2026-10-07', hasta: '2026-10-08' })

    expect(new Date(desde!).getTime()).toBe(local(2026, 10, 7))
    expect(new Date(hasta!).getTime()).toBe(local(2026, 10, 8, 23, 59, 59, 999))
  })

  it('an open-ended range only sends the end it has', () => {
    expect(filtroToParams({ tipo: 'rango', desde: '2026-10-07' }).hasta).toBeUndefined()
    expect(filtroToParams({ tipo: 'rango', hasta: '2026-10-07' }).desde).toBeUndefined()
  })
})

describe('filtroToListParams', () => {
  it('asks the list for the dashboard window, with the same bounds the summary uses', () => {
    const ahora = new Date(2026, 9, 8, 15, 0)
    const lista = filtroToListParams({ tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-02' }, ahora)
    const resumen = filtroToParams({ tipo: 'rango', desde: '2026-10-01', hasta: '2026-10-02' }, ahora)

    expect(lista.ventana).toBe(true)
    expect(lista.completadoDesde).toBe(resumen.desde)
    expect(lista.completadoHasta).toBe(resumen.hasta)
    expect(filtroToListParams({ tipo: 'todos' }).finalizados).toBe('todos')
  })
})

describe('ultimoResultado', () => {
  it('is the finish date when the caso has finished — the latest one, since a reprocess clears it', () => {
    expect(ultimoResultado({ completedAt: '2026-10-08T11:52:28Z', updatedAt: '2026-10-08T11:52:28Z' }).finalizadoAt).toBe('2026-10-08T11:52:28Z')
  })

  it('has no finish date while the caso is being reprocessed, but still says when it last did anything', () => {
    const r = ultimoResultado({ completedAt: null, updatedAt: '2026-10-08T11:19:59Z' })

    expect(r.finalizadoAt).toBeNull()
    expect(r.actividadAt).toBe('2026-10-08T11:19:59Z')
  })
})

describe('dates', () => {
  it('shows the year only when it is not the current one', () => {
    const ahora = new Date(2026, 9, 8)

    expect(formatFecha(new Date(2026, 9, 7, 16, 7).toISOString(), ahora)).not.toMatch(/2026/)
    expect(formatFecha(new Date(2025, 11, 31, 23, 59).toISOString(), ahora)).toMatch(/2025/)
  })

  it('says how long ago in the unit that fits', () => {
    const ahora = new Date(2026, 9, 8, 12, 0)
    const hace = (min: number) => new Date(ahora.getTime() - min * 60_000).toISOString()

    expect(haceCuanto(hace(0), ahora)).toBe('ahora mismo')
    expect(haceCuanto(hace(5), ahora)).toBe('hace 5 min')
    expect(haceCuanto(hace(180), ahora)).toBe('hace 3 h')
    expect(haceCuanto(hace(60 * 24 * 4), ahora)).toBe('hace 4 d')
  })

  it('says how long something took in the unit that fits', () => {
    expect(duracion(45_000)).toBe('45 s')
    expect(duracion(3 * 60_000)).toBe('3 min')
    expect(duracion(65 * 60_000)).toBe('1 h 05 min')
    expect(duracion(-5)).toBe('0 s')
  })
})
