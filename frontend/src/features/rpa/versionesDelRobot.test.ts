import { describe, expect, it } from 'vitest'
import { tamanoLegible } from './versionesDelRobot'

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
