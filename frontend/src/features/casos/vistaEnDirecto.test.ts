import { describe, expect, it } from 'vitest'
import { direccionDeVistaSegura, direccionParaElMarco } from './vistaEnDirecto'

describe('direccionDeVistaSegura', () => {
  it.each([
    ['http://localhost:6080/vnc.html', 'http://localhost:6080/vnc.html'],
    ['  https://vista.ejemplo.com/x?a=1 ', 'https://vista.ejemplo.com/x?a=1'],
  ])('acepta una dirección web completa: %s', (entrada, esperado) => {
    expect(direccionDeVistaSegura(entrada)).toBe(esperado)
  })

  it.each(['javascript:alert(1)', 'data:text/html,<script>1</script>', 'ftp://servidor/', '/relativa', 'vnc.html', '', null, undefined, 'http://'])(
    'rechaza lo que no es una dirección web: %s',
    (entrada) => {
      expect(direccionDeVistaSegura(entrada)).toBeNull()
    },
  )
})

describe('direccionParaElMarco', () => {
  it('con noVNC pide que se ajuste, que se conecte solo y que sea solo de mirar', () => {
    const direccion = new URL(direccionParaElMarco('http://localhost:6080/vnc.html'))

    expect(direccion.searchParams.get('autoconnect')).toBe('true')
    expect(direccion.searchParams.get('resize')).toBe('scale')
    expect(direccion.searchParams.get('view_only')).toBe('true')
  })

  it('otra dirección se deja como está', () => {
    expect(direccionParaElMarco('https://otro.ejemplo.com/pantalla')).toBe('https://otro.ejemplo.com/pantalla')
  })
})
