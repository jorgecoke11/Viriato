/**
 * What a failed session renewal means. Only the server saying the session is over (401) ends it. A server that is busy (429), one
 * that is restarting — it happens for a few seconds on every update — (502, 503) or no connection at all says nothing about the
 * session: it is asked again instead of sending the person back to the login.
 */
export function sesionTerminada(estado: number | undefined): boolean {
  return estado === 401
}

/** How long to wait before each new attempt to check the session on opening a page: some 15 seconds in all, enough for a restart. */
export const ESPERAS_AL_COMPROBAR_LA_SESION_MS = [1000, 2000, 4000, 8000]

function estadoDe(error: unknown): number | undefined {
  return typeof error === 'object' && error !== null && 'status' in error && typeof error.status === 'number' ? error.status : undefined
}

/** `pedir` until it works, the server says the session is over, or the waits run out; the last error is what is thrown. */
export async function comprobarSesion<T>(
  pedir: () => Promise<T>,
  esperar: (ms: number) => Promise<void> = (ms) => new Promise((resolver) => setTimeout(resolver, ms)),
): Promise<T> {
  for (let intento = 0; ; intento++) {
    try {
      return await pedir()
    } catch (error) {
      if (sesionTerminada(estadoDe(error)) || intento >= ESPERAS_AL_COMPROBAR_LA_SESION_MS.length) throw error
      await esperar(ESPERAS_AL_COMPROBAR_LA_SESION_MS[intento])
    }
  }
}
