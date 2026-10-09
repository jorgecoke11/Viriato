import { describe, expect, it } from 'vitest'
import type { CasoTimelineItemDto } from './api'
import { construirLinea, contarPorTipo, tipoDeEntrada } from './lineaDeTiempo'

const AHORA = new Date(2026, 9, 8, 18, 0)

const item = (id: string, minutosAntes: number, parcial: Partial<CasoTimelineItemDto> = {}): CasoTimelineItemDto => ({
  id,
  tipo: 'Evidencia',
  occurredAt: new Date(AHORA.getTime() - minutosAntes * 60_000).toISOString(),
  titulo: id,
  estadoCodigo: null,
  documentoId: null,
  evidenciaTipo: 'Otro',
  contenidoJson: null,
  pasoNombre: null,
  nombreArchivo: null,
  contentType: null,
  tamanoBytes: null,
  ...parcial,
})

const captura = (id: string, minutosAntes: number, paso: string | null = 'Extraer') =>
  item(id, minutosAntes, { evidenciaTipo: 'Screenshot', documentoId: `doc-${id}`, contentType: 'image/png', pasoNombre: paso })

describe('tipoDeEntrada', () => {
  it('tells apart what is looked at from what is downloaded', () => {
    expect(tipoDeEntrada(item('a', 1, { tipo: 'EstadoCambiado' }))).toBe('estado')
    expect(tipoDeEntrada(captura('b', 1))).toBe('captura')
    expect(tipoDeEntrada(item('c', 1, { evidenciaTipo: 'Video', documentoId: 'd', contentType: 'video/mp4' }))).toBe('video')
    expect(tipoDeEntrada(item('d', 1, { evidenciaTipo: 'ArchivoGenerado', documentoId: 'd', contentType: 'text/csv' }))).toBe('archivo')
    expect(tipoDeEntrada(item('e', 1, { evidenciaTipo: 'DatosExtraidos', contenidoJson: '{}' }))).toBe('datos')
    expect(tipoDeEntrada(item('f', 1))).toBe('nota')
  })

  it('a document someone uploaded is a file even when it is an image', () => {
    expect(tipoDeEntrada(item('g', 1, { tipo: 'Documento', documentoId: 'd', contentType: 'image/jpeg' }))).toBe('archivo')
  })
})

describe('contarPorTipo', () => {
  it('counts each kind and the total', () => {
    const cuenta = contarPorTipo([captura('a', 1), captura('b', 2), item('c', 3), item('d', 4, { tipo: 'EstadoCambiado' })])
    expect(cuenta).toMatchObject({ todo: 4, captura: 2, nota: 1, estado: 1, archivo: 0 })
  })
})

describe('construirLinea', () => {
  it('splits by day, newest first, and names today and yesterday', () => {
    const dias = construirLinea([item('ayer', 24 * 60), item('hoy', 5), item('antes', 3 * 24 * 60)], 'todo', AHORA)
    expect(dias.map((d) => d.etiqueta)).toEqual(['Hoy', 'Ayer', expect.any(String)])
    expect(dias[0].entradas[0]).toMatchObject({ id: 'hoy' })
  })

  it('narrows to one kind', () => {
    const dias = construirLinea([captura('a', 1), item('b', 2)], 'nota', AHORA)
    expect(dias.flatMap((d) => d.entradas).map((e) => e.id)).toEqual(['b'])
  })

  it('folds screenshots one after another from the same step into a gallery, in the order they were taken', () => {
    const [dia] = construirLinea([captura('c3', 1), captura('c2', 2), captura('c1', 3)], 'todo', AHORA)
    expect(dia.entradas).toHaveLength(1)
    const [entrada] = dia.entradas
    expect(entrada.clase).toBe('galeria')
    if (entrada.clase === 'galeria') expect(entrada.items.map((i) => i.id)).toEqual(['c1', 'c2', 'c3'])
  })

  it('a single screenshot stays a card, and a different step or another entry in between breaks the gallery', () => {
    const [dia] = construirLinea([captura('a', 1, 'Uno'), captura('b', 2, 'Dos'), item('nota', 3), captura('c', 4, 'Dos')], 'todo', AHORA)
    expect(dia.entradas.map((e) => e.clase)).toEqual(['item', 'item', 'item', 'item'])
  })
})
