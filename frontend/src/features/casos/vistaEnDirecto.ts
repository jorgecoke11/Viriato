/**
 * The address where a robot's screen can be watched is reported by the robot and ends up as the source of a frame and a link, so it
 * is only ever used if it is a complete web address (http or https). The server refuses anything else too; this is the second lock,
 * for whatever was stored before and for the day someone forgets the first.
 */
export function direccionDeVistaSegura(url: string | null | undefined): string | null {
  if (!url) return null
  try {
    const direccion = new URL(url.trim())
    return (direccion.protocol === 'http:' || direccion.protocol === 'https:') && direccion.hostname !== '' ? direccion.toString() : null
  } catch {
    return null
  }
}

/**
 * Whether the screen is served by Viriato's own address (under /vista/, through its Caddy, which is how it is seen from outside the
 * robot's PC). Viriato keeps that door: the browser needs a pass before it may load the screen. A screen anywhere else
 * (http://localhost:6080 on the robot's own PC) has no such door.
 */
export function necesitaPaseDeViriato(url: string, origen: string = window.location.origin): boolean {
  return new URL(url).origin === origen
}

/** The same screen, in a form made for a small frame: scaled to fit and without the controls that let a person touch it. */
export function direccionParaElMarco(url: string): string {
  const direccion = new URL(url)
  // noVNC's own options; another viewer simply ignores them.
  if (direccion.pathname.endsWith('vnc.html')) {
    direccion.searchParams.set('autoconnect', 'true')
    direccion.searchParams.set('resize', 'scale')
    direccion.searchParams.set('view_only', 'true')
  }
  return direccion.toString()
}
