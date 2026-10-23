import { describe, expect, it } from 'vitest'
import { calidadDeVista, calidadesDeVista, etiquetaDeGrabacion, opcionesDeGrabacion } from './pantallaDelRobot'

describe('pantalla del robot', () => {
  it('ofrece apagarla y tres calidades, de la más ligera a la más fluida', () => {
    expect(calidadesDeVista.map((c) => c.id)).toEqual(['Apagada', 'Baja', 'Media', 'Alta'])
  })

  it('una calidad desconocida se muestra como apagada en vez de romper la tabla', () => {
    expect(calidadDeVista('Media').titulo).toBe('Media')
    expect(calidadDeVista('Ultra').id).toBe('Apagada')
  })

  it('el vídeo puede dejarse al robot, encenderse o apagarse', () => {
    expect(opcionesDeGrabacion.map((o) => o.value)).toEqual(['SegunRobot', 'Activa', 'Apagada'])
    expect(etiquetaDeGrabacion('SegunRobot')).toBe('Lo decide el robot')
  })
})
