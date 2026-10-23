import { describe, expect, it } from 'vitest'
import type { EjecucionPasoDto, FlujoPasoDefDto } from './api'
import { etiquetaDeClave, leerDatos } from '../../lib/datos'
import { pasosResueltos, progresoDelProceso } from './progreso'

const definicion = (id: string, orden: number, nombre: string): FlujoPasoDefDto => ({
  id,
  flujoVersionId: 'v1',
  orden,
  nombre,
  tipoPaso: 'Rpa',
  agenteDefinicionId: null,
  servicioId: null,
  configuracionJson: null,
})

const intento = (flujoPasoDefId: string, numeroIntento: number, estado: string, enCola = false): EjecucionPasoDto => ({
  id: `${flujoPasoDefId}-${numeroIntento}`,
  ejecucionId: 'e1',
  flujoPasoDefId,
  tipoPaso: 'Rpa',
  numeroIntento,
  estado,
  trabajoId: null,
  errorMensaje: null,
  startedAt: null,
  finishedAt: null,
  prioridad: 0,
  enCola,
})

describe('progresoDelProceso', () => {
  const definiciones = [definicion('c', 3, 'Cerrar'), definicion('a', 1, 'Crear casos'), definicion('b', 2, 'Extraer precios')]

  it('lists the steps of the process in order, whatever order they come in', () => {
    expect(progresoDelProceso(definiciones, []).map((p) => p.nombre)).toEqual(['Crear casos', 'Extraer precios', 'Cerrar'])
  })

  it('a step that never ran is pending, with no attempts', () => {
    const [primero] = progresoDelProceso(definiciones, [])

    expect(primero).toMatchObject({ estado: 'pendiente', intentos: 0, ultimo: null })
  })

  it('reads each step from its state: done, skipped, waiting for a robot, running', () => {
    const progreso = progresoDelProceso(definiciones, [intento('a', 1, 'Omitido'), intento('b', 1, 'EnProgreso', true)])

    expect(progreso.map((p) => p.estado)).toEqual(['omitido', 'en-cola', 'pendiente'])
    expect(progresoDelProceso(definiciones, [intento('b', 1, 'EnProgreso', false)])[1].estado).toBe('en-curso')
    expect(progresoDelProceso(definiciones, [intento('a', 1, 'Completado')])[0].estado).toBe('completado')
    expect(progresoDelProceso(definiciones, [intento('a', 1, 'EsperandoRevisionHumana')])[0].estado).toBe('revision')
  })

  it('a step that failed and was reprocessed stands where its newest attempt is, and counts every attempt', () => {
    const progreso = progresoDelProceso(definiciones, [intento('b', 2, 'EnProgreso', true), intento('b', 1, 'Fallido')])

    expect(progreso[1]).toMatchObject({ estado: 'en-cola', intentos: 2 })
    expect(progreso[1].ultimo?.numeroIntento).toBe(2)
  })

  it('a failed step stays failed until it is reprocessed', () => {
    expect(progresoDelProceso(definiciones, [intento('b', 1, 'Fallido')])[1].estado).toBe('fallido')
  })

  it('counts as resolved the steps that are done or deliberately skipped', () => {
    const progreso = progresoDelProceso(definiciones, [intento('a', 1, 'Omitido'), intento('b', 1, 'Completado'), intento('c', 1, 'Fallido')])

    expect(pasosResueltos(progreso)).toBe(2)
  })
})

describe('etiquetaDeClave', () => {
  it.each([
    ['precioUnitario', 'Precio unitario'],
    ['precio_unitario', 'Precio unitario'],
    ['nombre-producto', 'Nombre producto'],
    ['beneficio', 'Beneficio'],
    ['URL', 'Url'],
  ])('reads %s as %s', (clave, esperada) => {
    expect(etiquetaDeClave(clave)).toBe(esperada)
  })
})

describe('leerDatos', () => {
  it('says there is nothing for no data, an empty text or an empty object', () => {
    expect(leerDatos(null)).toEqual({ tipo: 'vacio' })
    expect(leerDatos('  ')).toEqual({ tipo: 'vacio' })
    expect(leerDatos('{}')).toEqual({ tipo: 'vacio' })
  })

  it('reads an object as one entry per field, keeping their order and giving each a readable label', () => {
    const datos = leerDatos('{"proveedor":"Balay","beneficio":15,"activo":true,"nota":null}')

    expect(datos.tipo).toBe('entradas')
    if (datos.tipo !== 'entradas') return
    expect(datos.entradas.map((e) => e.etiqueta)).toEqual(['Proveedor', 'Beneficio', 'Activo', 'Nota'])
    expect(datos.entradas.map((e) => e.valor)).toEqual([
      { tipo: 'texto', texto: 'Balay' },
      { tipo: 'numero', texto: '15' },
      { tipo: 'booleano', texto: 'Sí' },
      { tipo: 'vacio' },
    ])
  })

  it('folds lists and nested objects, saying what they hold', () => {
    const datos = leerDatos('{"productos":["a","b","c"],"cliente":{"id":1,"nombre":"x"},"uno":[1]}')

    if (datos.tipo !== 'entradas') throw new Error('expected entries')
    const [productos, cliente, uno] = datos.entradas.map((e) => e.valor)
    expect(productos).toMatchObject({ tipo: 'compuesto', resumen: '3 elementos' })
    expect(cliente).toMatchObject({ tipo: 'compuesto', resumen: '2 campos' })
    expect(uno).toMatchObject({ tipo: 'compuesto', resumen: '1 elemento' })
    expect(JSON.parse((productos as { json: string }).json)).toEqual(['a', 'b', 'c'])
  })

  it('gives a list of records a table, and only a list of records', () => {
    const datos = leerDatos('{"puntos":[{"fecha":"2026-10-21","precio":199.99},{"fecha":"2026-10-22","precio":149}],"tags":["a"],"mixta":[{"a":1},2]}')

    if (datos.tipo !== 'entradas') throw new Error('expected entries')
    const [puntos, tags, mixta] = datos.entradas.map((e) => e.valor)
    expect(puntos).toMatchObject({
      tipo: 'compuesto',
      tabla: {
        columnas: [
          { clave: 'fecha', etiqueta: 'Fecha' },
          { clave: 'precio', etiqueta: 'Precio' },
        ],
        filas: [
          ['2026-10-21', '199.99'],
          ['2026-10-22', '149'],
        ],
      },
    })
    expect((tags as { tabla?: unknown }).tabla).toBeUndefined()
    expect((mixta as { tabla?: unknown }).tabla).toBeUndefined()
  })

  it('shows as it is what is not a JSON object', () => {
    expect(leerDatos('no es json')).toEqual({ tipo: 'crudo', texto: 'no es json' })
    expect(leerDatos('[1,2]')).toMatchObject({ tipo: 'crudo' })
    expect(leerDatos('42')).toEqual({ tipo: 'crudo', texto: '42' })
  })
})
