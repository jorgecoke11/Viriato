import { describe, expect, it } from 'vitest'
import { aDatos, aEstado, contarCampos, estadoInicial, inferirEsquema, leerEsquema, validarDatos } from './esquema'
import type { Esquema } from './esquema'

// The same cases as the server's EsquemaDatosTests (backend/tests/Viariato.Modules.Flujos.Tests): the form and the
// API must agree on what is acceptable, so a table that changes in one place changes in the other.

const CREADOR = `{
  "type": "object",
  "required": ["beneficio"],
  "properties": {
    "beneficio": { "type": "number", "title": "Beneficio", "minimum": 0, "maximum": 100, "x-suffix": "%" },
    "actualizar": { "type": "boolean", "title": "Actualizar precios", "default": false }
  }
}`

const esquemaDe = (texto: string): Esquema => {
  const r = leerEsquema(texto)
  if (!r.ok) throw new Error(r.errores.join(' | '))
  return r.esquema
}

const unCampo = (campo: string, requerido = false) =>
  `{ "type": "object", "required": [${requerido ? '"x"' : ''}], "properties": { "x": ${campo} } }`

const errores = (esquema: string, datos: unknown) => validarDatos(esquemaDe(esquema), datos).map((e) => e.completo)

describe('leerEsquema', () => {
  it('accepts a schema with every kind of field', () => {
    const completo = `{
      "type": "object", "required": ["nombre"],
      "properties": {
        "nombre": { "type": "string", "minLength": 2, "maxLength": 50, "pattern": "^[A-Z]" },
        "notas": { "type": "string", "x-widget": "textarea" },
        "alta": { "type": "string", "format": "date" },
        "momento": { "type": "string", "format": "date-time" },
        "correo": { "type": "string", "format": "email" },
        "estado": { "type": "string", "enum": ["a", "b"], "enumNames": ["A", "B"], "default": "a" },
        "edad": { "type": "integer", "minimum": 0, "maximum": 120 },
        "nivel": { "type": "integer", "enum": [1, 2, 3] },
        "precio": { "type": "number", "x-suffix": "€" },
        "activo": { "type": "boolean" },
        "etiquetas": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 5 },
        "tallas": { "type": "array", "items": { "type": "string", "enum": ["S", "M", "L"] } },
        "lineas": { "type": "array", "items": { "type": "object", "required": ["sku"], "properties": {
          "sku": { "type": "string" }, "unidades": { "type": "integer", "minimum": 1 } } } },
        "direccion": { "type": "object", "properties": { "calle": { "type": "string" } } }
      }
    }`
    expect(leerEsquema(completo).ok).toBe(true)
    expect(leerEsquema(CREADOR).ok).toBe(true)
  })

  it.each<[string, string]>([
    ['', 'vacío'],
    ['   ', 'vacío'],
    ['{no es json', 'JSON válido'],
    ['[1]', 'objeto JSON'],
    ['{"type":"array","properties":{"a":{"type":"string"}}}', 'raíz'],
    ['{"type":"object"}', 'properties'],
    ['{"type":"object","properties":{}}', 'ningún campo'],
    ['{"type":"object","properties":{"a":"string"}}', 'debe ser un objeto'],
    ['{"type":"object","properties":{"a":{}}}', 'falta "type"'],
    ['{"type":"object","properties":{"a":{"type":"uuid"}}}', 'no está soportado'],
    ['{"type":"object","properties":{"a":{"type":"string"}},"required":["b"]}', 'no existe'],
    ['{"type":"object","properties":{"a":{"type":"string"}},"required":"a"}', 'lista de nombres'],
    ['{"type":"object","properties":{"a":{"type":"string","title":3}}}', '"title" debe ser texto'],
    ['{"type":"object","properties":{"a":{"type":"string","minLength":5,"maxLength":2}}}', 'minLength'],
    ['{"type":"object","properties":{"a":{"type":"string","minLength":-1}}}', 'mayor o igual que 0'],
    ['{"type":"object","properties":{"a":{"type":"string","pattern":"("}}}', 'expresión regular'],
    ['{"type":"object","properties":{"a":{"type":"string","format":"uuid"}}}', '"format"'],
    ['{"type":"object","properties":{"a":{"type":"string","x-widget":"slider"}}}', 'x-widget'],
    ['{"type":"object","properties":{"a":{"type":"string","enum":[]}}}', 'al menos una opción'],
    ['{"type":"object","properties":{"a":{"type":"string","enum":["x",1]}}}', 'no es de tipo string'],
    ['{"type":"object","properties":{"a":{"type":"integer","enum":[1.5]}}}', 'no es de tipo integer'],
    ['{"type":"object","properties":{"a":{"type":"string","enum":["x","y"],"enumNames":["X"]}}}', 'enumNames'],
    ['{"type":"object","properties":{"a":{"type":"number","minimum":10,"maximum":1}}}', 'minimum'],
    ['{"type":"object","properties":{"a":{"type":"number","minimum":"1"}}}', 'debe ser un número'],
    ['{"type":"object","properties":{"a":{"type":"array"}}}', 'falta "items"'],
    ['{"type":"object","properties":{"a":{"type":"array","items":{"type":"array","items":{"type":"string"}}}}}', 'los elementos de la lista'],
    ['{"type":"object","properties":{"a":{"type":"array","items":{"type":"boolean"}}}}', 'los elementos de la lista'],
    ['{"type":"object","properties":{"a":{"type":"array","items":{"type":"string"},"minItems":3,"maxItems":1}}}', 'minItems'],
    ['{"type":"object","properties":{"a":{"type":"object"}}}', 'properties'],
    ['{"type":"object","properties":{"a":{"type":"integer","default":1.5}}}', 'valor por defecto'],
    ['{"type":"object","properties":{"a":{"type":"string","enum":["x"],"default":"z"}}}', 'valor por defecto'],
  ])('rejects %s, saying why', (esquema, fragmento) => {
    const r = leerEsquema(esquema)
    expect(r.ok).toBe(false)
    if (!r.ok) expect(r.errores.some((e) => e.toLowerCase().includes(fragmento.toLowerCase()))).toBe(true)
  })

  it('names the path of an error in a nested field', () => {
    const r = leerEsquema('{"type":"object","properties":{"lineas":{"type":"array","items":{"type":"object","properties":{"sku":{"type":"uuid"}}}}}}')
    expect(!r.ok && r.errores.some((e) => e.startsWith('lineas[].sku:'))).toBe(true)
  })

  it('rejects an object nested too deep, too many fields and a schema that is too long', () => {
    const hondo = '{"type":"object","properties":{"a":{"type":"object","properties":{"b":{"type":"object","properties":{"c":{"type":"object","properties":{"d":{"type":"object","properties":{"e":{"type":"string"}}}}}}}}}}}'
    const r1 = leerEsquema(hondo)
    expect(!r1.ok && r1.errores.some((e) => e.includes('anidado'))).toBe(true)

    const campos = Array.from({ length: 101 }, (_, i) => `"c${i}":{"type":"string"}`).join(',')
    const r2 = leerEsquema(`{"type":"object","properties":{${campos}}}`)
    expect(!r2.ok && r2.errores.some((e) => e.includes('Demasiados campos'))).toBe(true)

    const r3 = leerEsquema('x'.repeat(20_001))
    expect(!r3.ok && r3.errores.some((e) => e.includes('demasiado largo'))).toBe(true)
  })

  it('ignores keywords outside the supported subset', () => {
    expect(
      leerEsquema('{"$schema":"x","type":"object","additionalProperties":false,"properties":{"a":{"type":"string","examples":["x"],"readOnly":true}}}').ok,
    ).toBe(true)
  })
})

describe('validarDatos', () => {
  it('accepts data that fits', () => {
    expect(errores(CREADOR, { beneficio: 15, actualizar: true })).toEqual([])
    expect(errores(CREADOR, { beneficio: 0 })).toEqual([])
    expect(errores(CREADOR, { beneficio: 15.5, otro: 'extra' })).toEqual([])
  })

  it.each<unknown>([{}, { beneficio: null }, { beneficio: '' }])('reports a missing required field by its title (%j)', (datos) => {
    expect(errores(CREADOR, datos)).toEqual(['Beneficio: es obligatorio.'])
  })

  it.each<[unknown, string]>([
    [{ beneficio: '15' }, 'debe ser un número'],
    [{ beneficio: -1 }, 'como mínimo 0'],
    [{ beneficio: 101 }, 'como máximo 100'],
    [{ beneficio: 5, actualizar: 'si' }, 'verdadero o falso'],
    [[1], 'objeto JSON'],
    ['texto', 'objeto JSON'],
  ])('rejects %j (%s)', (datos, fragmento) => {
    expect(errores(CREADOR, datos).some((e) => e.includes(fragmento))).toBe(true)
  })

  it('reports every problem, not just the first', () => {
    const e = errores('{"type":"object","required":["a","b"],"properties":{"a":{"type":"string"},"b":{"type":"integer"},"c":{"type":"boolean"}}}', { c: 'x' })
    expect(e).toHaveLength(3)
  })

  it.each<[string, unknown, string]>([
    ['{"type":"string","minLength":3}', 'ab', 'al menos 3'],
    ['{"type":"string","maxLength":3}', 'abcd', 'como máximo 3'],
    ['{"type":"string","pattern":"^[A-Z]{2}\\\\d+$"}', 'x1', 'formato esperado'],
    ['{"type":"string","format":"date"}', '2026-13-40', 'fecha válida'],
    ['{"type":"string","format":"date"}', '07/10/2026', 'fecha válida'],
    ['{"type":"string","format":"date-time"}', 'ayer', 'fecha y hora'],
    ['{"type":"string","format":"email"}', 'sin-arroba', 'correo'],
    ['{"type":"string","enum":["a","b"]}', 'c', 'uno de: a, b'],
    ['{"type":"integer"}', 1.5, 'entero'],
    ['{"type":"integer","enum":[1,2]}', 3, 'uno de: 1, 2'],
    ['{"type":"string"}', 5, 'debe ser texto'],
    ['{"type":"boolean"}', 'true', 'verdadero o falso'],
    ['{"type":"array","items":{"type":"string"},"minItems":2}', ['a'], 'al menos 2'],
    ['{"type":"array","items":{"type":"string"},"maxItems":1}', ['a', 'b'], 'como máximo 1'],
    ['{"type":"array","items":{"type":"string"}}', 'a', 'debe ser una lista'],
    ['{"type":"array","items":{"type":"string"}}', ['a', 5], '#2: debe ser texto'],
    ['{"type":"array","items":{"type":"string"}}', ['a', ''], '#2: es obligatorio'],
    ['{"type":"object","properties":{"k":{"type":"string"}},"required":["k"]}', {}, 'k: es obligatorio'],
    ['{"type":"object","properties":{"k":{"type":"string"}}}', [], 'debe ser un objeto'],
  ])('enforces %s on %j', (campo, valor, fragmento) => {
    expect(errores(unCampo(campo), { x: valor }).some((e) => e.includes(fragmento))).toBe(true)
  })

  it.each<[string, unknown]>([
    ['{"type":"string","minLength":3}', 'abc'],
    ['{"type":"string","pattern":"^[A-Z]{2}\\\\d+$"}', 'AB12'],
    ['{"type":"string","format":"date"}', '2026-10-07'],
    ['{"type":"string","format":"date-time"}', '2026-10-07T12:30:00Z'],
    ['{"type":"string","format":"email"}', 'a@b.com'],
    ['{"type":"string","enum":["a","b"]}', 'b'],
    ['{"type":"integer"}', 3],
    ['{"type":"number","minimum":0.5}', 0.5],
    ['{"type":"boolean"}', false],
    ['{"type":"array","items":{"type":"string"},"minItems":2}', ['a', 'b']],
  ])('passes %s on %j', (campo, valor) => {
    expect(errores(unCampo(campo), { x: valor })).toEqual([])
  })

  it('treats an empty optional string as not filled in', () => {
    expect(errores(unCampo('{"type":"string","minLength":3}'), { x: '' })).toEqual([])
  })

  it('treats a required empty list as missing', () => {
    expect(errores(unCampo('{"type":"array","items":{"type":"string"}}', true), { x: [] })).toEqual(['x: es obligatorio.'])
  })

  it('names nested errors with the labels of the path, and their dotted path', () => {
    const esquema = `{"type":"object","properties":{
      "lineas":{"type":"array","title":"Líneas","items":{"type":"object","required":["sku"],"properties":{
        "sku":{"type":"string","title":"SKU"},"unidades":{"type":"integer","title":"Unidades","minimum":1}}}},
      "direccion":{"type":"object","title":"Dirección","required":["calle"],"properties":{"calle":{"type":"string","title":"Calle"}}}}}`

    const resultado = validarDatos(esquemaDe(esquema), { lineas: [{ sku: 'A', unidades: 2 }, { unidades: 0 }], direccion: {} })

    expect(resultado.map((e) => e.completo)).toEqual([
      'Líneas #2 › SKU: es obligatorio.',
      'Líneas #2 › Unidades: debe ser como mínimo 1.',
      'Dirección › Calle: es obligatorio.',
    ])
    expect(resultado.map((e) => e.ruta)).toEqual(['lineas.1.sku', 'lineas.1.unidades', 'direccion.calle'])
  })
})

describe('form state', () => {
  const esquema = esquemaDe(`{
    "type": "object", "required": ["beneficio"],
    "properties": {
      "beneficio": { "type": "number" }, "actualizar": { "type": "boolean", "default": true },
      "producto": { "type": "string" }, "etiquetas": { "type": "array", "items": { "type": "string" } },
      "lineas": { "type": "array", "items": { "type": "object", "properties": { "sku": { "type": "string" }, "n": { "type": "integer" } } } },
      "dir": { "type": "object", "properties": { "calle": { "type": "string" } } }
    }
  }`)

  it('starts empty, with defaults filled in, and every field present', () => {
    expect(estadoInicial(esquema)).toEqual({ beneficio: '', actualizar: true, producto: '', etiquetas: [], lineas: [], dir: { calle: '' } })
  })

  it('sends booleans always and leaves out whatever is empty', () => {
    expect(aDatos(esquema, estadoInicial(esquema))).toEqual({ actualizar: true })
  })

  it('turns typed numbers into numbers (comma or dot), and drops blank rows of a list', () => {
    const estado = {
      ...estadoInicial(esquema),
      beneficio: '15,5',
      etiquetas: ['uno', '', 'dos'],
      lineas: [{ sku: 'A', n: '2' }, { sku: '', n: '' }],
      dir: { calle: 'Mayor' },
    }

    expect(aDatos(esquema, estado)).toEqual({
      beneficio: 15.5,
      actualizar: true,
      etiquetas: ['uno', 'dos'],
      lineas: [{ sku: 'A', n: 2 }],
      dir: { calle: 'Mayor' },
    })
  })

  it('keeps a half-typed number as text so validation can flag it instead of dropping it', () => {
    const datos = aDatos(esquema, { ...estadoInicial(esquema), beneficio: '1e' })

    expect(datos.beneficio).toBe('1e')
    expect(validarDatos(esquema, datos).some((e) => e.mensaje.includes('número'))).toBe(true)
  })

  it('loads existing data into the form, converting numbers to text', () => {
    const estado = aEstado(esquema, { beneficio: 15, producto: 'Lavadoras', etiquetas: ['a'], lineas: [{ sku: 'X', n: 3 }] })

    expect(estado).toMatchObject({ beneficio: '15', producto: 'Lavadoras', etiquetas: ['a'], lineas: [{ sku: 'X', n: '3' }] })
  })

  it('keeps the keys of the document the schema does not describe', () => {
    const base = { beneficio: 1, extra: { a: 1 }, otro: 'x' }

    expect(aDatos(esquema, aEstado(esquema, base), base)).toMatchObject({ beneficio: 1, extra: { a: 1 }, otro: 'x' })
  })
})

describe('inferirEsquema', () => {
  it('takes the type of each field from the example value', () => {
    const esquema = inferirEsquema({
      beneficio: 15,
      precio: 9.5,
      actualizar: true,
      producto: 'Lavadoras',
      alta: '2026-10-07',
      correo: 'a@b.com',
      etiquetas: ['a', 'b'],
      cantidades: [1, 2],
      lineas: [{ sku: 'A', unidades: 2 }],
      direccion: { calle: 'Mayor' },
    })!

    expect(esquema.type).toBe('object')
    expect(esquema.properties).toMatchObject({
      beneficio: { type: 'number' },
      precio: { type: 'number' },
      actualizar: { type: 'boolean' },
      producto: { type: 'string' },
      alta: { type: 'string', format: 'date' },
      correo: { type: 'string', format: 'email' },
      etiquetas: { type: 'array', items: { type: 'string' } },
      cantidades: { type: 'array', items: { type: 'number' } },
      lineas: { type: 'array', items: { type: 'object', properties: { sku: { type: 'string' }, unidades: { type: 'number' } } } },
      direccion: { type: 'object', properties: { calle: { type: 'string' } } },
    })
  })

  it('gives each field a readable title from its key', () => {
    const esquema = inferirEsquema({ n_maximo_carrito: 14, fechaAlta: 'x' })!

    expect(esquema.properties.n_maximo_carrito.title).toBe('N maximo carrito')
    expect(esquema.properties.fechaAlta.title).toBe('Fecha Alta')
  })

  it('produces a schema the platform accepts, and a form that accepts the example itself', () => {
    const ejemplo = { beneficio: 15, actualizar: true, producto: 'Lavadoras', etiquetas: ['a'], lineas: [{ sku: 'A', unidades: 2 }] }
    const esquema = inferirEsquema(ejemplo)!

    const leido = leerEsquema(JSON.stringify(esquema))
    expect(leido.ok).toBe(true)
    expect(validarDatos(esquema, ejemplo)).toEqual([])
    expect(aDatos(esquema, aEstado(esquema, ejemplo))).toEqual(ejemplo)
  })

  it.each([null, 5, 'texto', [1], {}])('returns nothing for %j (not an object with fields)', (ejemplo) => {
    expect(inferirEsquema(ejemplo)).toBeNull()
  })
})

describe('contarCampos', () => {
  it('counts fields at any depth, an array as one field', () => {
    expect(contarCampos(esquemaDe(CREADOR))).toBe(2)
    expect(contarCampos(esquemaDe('{"type":"object","properties":{"a":{"type":"string"},"d":{"type":"object","properties":{"x":{"type":"string"},"y":{"type":"string"}}},"l":{"type":"array","items":{"type":"string"}}}}'))).toBe(4)
  })
})
