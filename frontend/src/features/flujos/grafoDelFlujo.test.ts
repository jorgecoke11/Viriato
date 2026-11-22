import { describe, expect, it } from 'vitest'
import type { FlujoPasoDefDto, FlujoVersionDetailDto } from './api'
import { alcanzables, avisos, flechasRepetidas, grafoDesdeVersion, INICIO, ordenar, peticionDesdeGrafo, type Grafo } from './grafoDelFlujo'

const paso = (id: string, orden: number, extra: Partial<FlujoPasoDefDto> = {}): FlujoPasoDefDto => ({
  id,
  flujoVersionId: 'v',
  orden,
  nombre: id,
  tipoPaso: 'Rpa',
  agenteDefinicionId: null,
  servicioId: `s-${id}`,
  configuracionJson: '{"aplicacion":"Portal"}',
  posicionX: null,
  posicionY: null,
  maximoDeVisitas: null,
  ...extra,
})

const version = (pasos: FlujoPasoDefDto[], transiciones: FlujoVersionDetailDto['transiciones'] = []): FlujoVersionDetailDto => ({
  id: 'v',
  flujoId: 'f',
  numeroVersion: 1,
  estado: 'Borrador',
  notas: null,
  createdAt: '',
  publishedAt: null,
  pasos,
  transiciones,
})

describe('grafoDesdeVersion', () => {
  it('turns a list-shaped version into the arrows it already behaves like', () => {
    const grafo = grafoDesdeVersion(version([paso('b', 2), paso('a', 1)]))!

    expect(grafo.flechas.map((f) => [f.origen, f.destino, f.resultado, f.estado])).toEqual([
      [INICIO, 'a', null, null],
      ['a', 'b', 'Completada', null],
    ])
    // A Rpa step of the arrow-shaped flow has no configuration.
    expect(grafo.pasos.every((p) => p.configuracionJson === null)).toBe(true)
  })

  it('cannot carry over a list-shaped version with Decision steps', () => {
    expect(grafoDesdeVersion(version([paso('a', 1), paso('d', 2, { tipoPaso: 'Decision', servicioId: null })]))).toBeNull()
  })

  it('reads a version with arrows as it is', () => {
    const grafo = grafoDesdeVersion(
      version(
        [paso('a', 1, { posicionX: 10, posicionY: 20 }), paso('b', 2)],
        [
          { id: 't1', pasoOrigenId: null, resultado: null, estadoNegocioId: null, estadoNegocioCodigo: null, pasoDestinoId: 'a' },
          { id: 't2', pasoOrigenId: 'a', resultado: 'Completada', estadoNegocioId: 'e', estadoNegocioCodigo: 'PRECIOS_EXTRAIDOS', pasoDestinoId: 'b' },
        ],
      ),
    )!

    expect(grafo.pasos[0]).toMatchObject({ x: 10, y: 20 })
    expect(grafo.flechas[1]).toMatchObject({ origen: 'a', destino: 'b', resultado: 'Completada', estado: 'PRECIOS_EXTRAIDOS' })
  })
})

const p01: Grafo = {
  pasos: [
    { clave: 'act', nombre: 'Actualizar', tipoPaso: 'Rpa', servicioId: 's2', agenteDefinicionId: null, configuracionJson: null, maximoDeVisitas: null, x: 600, y: 0 },
    { clave: 'ext', nombre: 'Extraer', tipoPaso: 'Rpa', servicioId: 's1', agenteDefinicionId: null, configuracionJson: null, maximoDeVisitas: 3, x: 300, y: 0 },
  ],
  flechas: [
    { id: 'f1', origen: INICIO, destino: 'ext', resultado: null, estado: null },
    { id: 'f2', origen: 'ext', destino: 'act', resultado: 'Completada', estado: ' PRECIOS_EXTRAIDOS ' },
  ],
}

describe('peticionDesdeGrafo', () => {
  it('numbers the steps left to right and sends the arrows by clave', () => {
    const peticion = peticionDesdeGrafo(p01)

    expect(peticion.pasos.map((p) => [p.clave, p.orden, p.maximoDeVisitas])).toEqual([
      ['ext', 1, 3],
      ['act', 2, null],
    ])
    expect(peticion.transiciones).toEqual([
      { origen: null, destino: 'ext', resultado: null, estadoNegocioCodigo: null },
      { origen: 'ext', destino: 'act', resultado: 'Completada', estadoNegocioCodigo: 'PRECIOS_EXTRAIDOS' },
    ])
  })
})

describe('checks', () => {
  it('finds the steps no arrow reaches, as a warning that does not block saving', () => {
    const grafo: Grafo = { ...p01, flechas: [p01.flechas[0]] }

    expect([...alcanzables(grafo)]).toEqual(['ext'])
    const aviso = avisos(grafo).find((a) => a.objetivo === 'act')
    expect(aviso).toMatchObject({ bloquea: false })
  })

  it('flags two arrows for the same step, result and state, and a missing start arrow', () => {
    const grafo: Grafo = {
      ...p01,
      flechas: [
        { id: 'x', origen: 'ext', destino: 'act', resultado: 'Fallida', estado: null },
        { id: 'y', origen: 'ext', destino: 'ext', resultado: 'Fallida', estado: '' },
      ],
    }

    expect(flechasRepetidas(grafo)).toEqual(new Set(['x', 'y']))
    expect(avisos(grafo).filter((a) => a.bloquea).map((a) => a.objetivo)).toEqual(expect.arrayContaining([INICIO, 'x', 'y']))
  })

  it('counts a step a creator starts at as an entry, so a flow of its own is not a dead step', () => {
    const conCreador: Grafo = {
      ...p01,
      pasos: [
        ...p01.pasos,
        { clave: 'cre', nombre: 'Crear casos', tipoPaso: 'Rpa', servicioId: 's3', agenteDefinicionId: null, configuracionJson: null, maximoDeVisitas: null, x: 0, y: 200 },
      ],
    }

    expect(avisos(conCreador).some((a) => a.objetivo === 'cre')).toBe(true)
    expect(avisos(conCreador, [{ creador: 'Masivo', paso: ' crear casos ' }]).some((a) => a.objetivo === 'cre')).toBe(false)
  })

  it('warns about a creator whose step is not in the flow', () => {
    const lista = avisos(p01, [{ creador: 'Crear Balay', paso: 'Extraer precios' }])

    expect(lista.find((a) => a.texto.includes('Crear Balay'))).toMatchObject({ objetivo: null, bloquea: false })
  })

  it('lays the steps out by distance from the start', () => {
    const ordenado = ordenar(p01)

    expect(ordenado.pasos.find((p) => p.clave === 'ext')).toMatchObject({ x: 300, y: 0 })
    expect(ordenado.pasos.find((p) => p.clave === 'act')).toMatchObject({ x: 600, y: 0 })
  })
})
