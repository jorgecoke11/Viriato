import { describe, expect, it } from 'vitest'
import { comprobarSesion, ESPERAS_AL_COMPROBAR_LA_SESION_MS, sesionTerminada } from './sesion'

const conEstado = (status: number) => Object.assign(new Error(`HTTP ${status}`), { status })

describe('sesionTerminada', () => {
  it('solo un 401 termina la sesión', () => {
    expect(sesionTerminada(401)).toBe(true)
  })

  it.each([429, 500, 502, 503, undefined])('%s no dice nada de la sesión', (estado) => {
    expect(sesionTerminada(estado)).toBe(false)
  })
})

describe('comprobarSesion', () => {
  const sinEsperar = async () => {}

  it('si el servidor está reiniciándose, vuelve a preguntar hasta que contesta', async () => {
    const respuestas = [conEstado(502), new TypeError('Failed to fetch'), conEstado(429)]
    let llamadas = 0
    const pedir = async () => {
      llamadas++
      const siguiente = respuestas.shift()
      if (siguiente) throw siguiente
      return 'sesión'
    }

    await expect(comprobarSesion(pedir, sinEsperar)).resolves.toBe('sesión')
    expect(llamadas).toBe(4)
  })

  it('un 401 se acepta a la primera: la sesión terminó', async () => {
    let llamadas = 0
    const pedir = async () => {
      llamadas++
      throw conEstado(401)
    }

    await expect(comprobarSesion(pedir, sinEsperar)).rejects.toMatchObject({ status: 401 })
    expect(llamadas).toBe(1)
  })

  it('si nunca contesta, se rinde tras las esperas previstas', async () => {
    const esperas: number[] = []
    let llamadas = 0
    const pedir = async () => {
      llamadas++
      throw conEstado(503)
    }

    await expect(comprobarSesion(pedir, async (ms) => void esperas.push(ms))).rejects.toMatchObject({ status: 503 })
    expect(esperas).toEqual(ESPERAS_AL_COMPROBAR_LA_SESION_MS)
    expect(llamadas).toBe(ESPERAS_AL_COMPROBAR_LA_SESION_MS.length + 1)
  })
})
