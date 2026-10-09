import { describe, expect, it } from 'vitest'
import { ejecutarEnTandas } from '../../lib/accionesMasivas'
import { leerResumenDeZip, nombreDeContentDisposition, nombreDelZip } from './resumenDeZip'

const aCabecera = (objeto: unknown) => btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(objeto))))

describe('leerResumenDeZip', () => {
  it('lee el resumen del servidor, con acentos incluidos', () => {
    const resumen = leerResumenDeZip(
      aCabecera({ documentos: 3, casosConDocumentos: 2, omitidosTotal: 1, omitidos: [{ id: 'a', motivo: 'No tiene documentos. Ñandú «x»' }] }),
    )

    expect(resumen).toEqual({
      documentos: 3,
      casosConDocumentos: 2,
      omitidosTotal: 1,
      omitidos: [{ id: 'a', motivo: 'No tiene documentos. Ñandú «x»' }],
    })
  })

  it.each([null, '', 'esto no es base64!', btoa('no es json'), btoa('[1,2]')])('un resumen ausente o estropeado (%s) es «sin nada que decir»', (cabecera) => {
    expect(leerResumenDeZip(cabecera)).toEqual({ documentos: 0, casosConDocumentos: 0, omitidosTotal: 0, omitidos: [] })
  })

  it('descarta los omitidos mal formados sin romperse', () => {
    const resumen = leerResumenDeZip(aCabecera({ omitidosTotal: 3, omitidos: [{ id: 'a', motivo: 'ok' }, { id: 5 }, null, 'x'] }))

    expect(resumen.omitidos).toEqual([{ id: 'a', motivo: 'ok' }])
    expect(resumen.omitidosTotal).toBe(3)
  })
})

describe('nombreDelZip', () => {
  it('sin partes, el nombre del servidor tal cual', () => {
    expect(nombreDelZip('documentos-20261008-2200.zip', 1, 1)).toBe('documentos-20261008-2200.zip')
  })

  it('con varias, numera cada una', () => {
    expect(nombreDelZip('documentos-20261008-2200.zip', 2, 3)).toBe('documentos-20261008-2200-parte-2-de-3.zip')
  })

  it('sin nombre del servidor, usa uno propio', () => {
    expect(nombreDelZip(null, 1, 1)).toBe('documentos.zip')
  })
})

describe('nombreDeContentDisposition', () => {
  it.each([
    ['attachment; filename=documentos-1.zip; filename*=UTF-8\'\'documentos-1.zip', 'documentos-1.zip'],
    ['attachment; filename="documentos 2.zip"', 'documentos 2.zip'],
    ["attachment; filename*=UTF-8''docs%20%C3%B1.zip", 'docs ñ.zip'],
    ['attachment', null],
    [null, null],
  ])('%s', (cabecera, esperado) => {
    expect(nombreDeContentDisposition(cabecera)).toBe(esperado)
  })
})

describe('ejecutarEnTandas con cifras propias', () => {
  it('suma los datos de cada tanda', async () => {
    const resultado = await ejecutarEnTandas(['a', 'b', 'c'], 2, async (ids) => ({ procesados: ids.length, omitidos: [], datos: { documentos: ids.length * 10 } }))

    expect(resultado.procesados).toBe(3)
    expect(resultado.datos).toEqual({ documentos: 30 })
  })

  it('sin datos propios no inventa ninguno', async () => {
    const resultado = await ejecutarEnTandas(['a'], 5, async () => ({ procesados: 1, omitidos: [] }))

    expect(resultado.datos).toBeUndefined()
  })
})
