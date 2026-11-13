import { describe, expect, it } from 'vitest'
import { numeroOpcional, tamanoLegible } from './versionesDelRobot'

describe('numeroOpcional', () => {
  it('reads empty as "what the version says"', () => {
    expect(numeroOpcional('')).toBeNull()
    expect(numeroOpcional('   ')).toBeNull()
  })

  it('reads whole numbers, and flags anything else', () => {
    expect(numeroOpcional(' 2 ')).toBe(2)
    expect(numeroOpcional('0')).toBe(0)
    expect(numeroOpcional('1.5')).toBeNaN()
    expect(numeroOpcional('dos')).toBeNaN()
  })
})

describe('tamanoLegible', () => {
  it('says KB below a megabyte, never 0', () => {
    expect(tamanoLegible(10)).toBe('1 KB')
    expect(tamanoLegible(830 * 1024)).toBe('830 KB')
  })

  it('says MB with one decimal, the Spanish way', () => {
    expect(tamanoLegible(12_021_492)).toBe('11,5 MB')
    expect(tamanoLegible(25 * 1024 * 1024)).toBe('25 MB')
  })
})
