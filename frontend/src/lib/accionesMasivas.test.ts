import { describe, expect, it } from 'vitest'
import { ejecutarEnTandas, type ResultadoMasivo } from './accionesMasivas'

const ids = (n: number) => Array.from({ length: n }, (_, i) => `id-${i}`)

describe('ejecutarEnTandas', () => {
  it('splits the selection into batches of the given size, in order', async () => {
    const recibidas: string[][] = []

    await ejecutarEnTandas(ids(7), 3, async (tanda) => {
      recibidas.push(tanda)
      return { procesados: tanda.length, omitidos: [] }
    })

    expect(recibidas).toEqual([ids(7).slice(0, 3), ids(7).slice(3, 6), ['id-6']])
  })

  it('adds up what every batch did, keeping the reasons for the ones left out', async () => {
    const resultado = await ejecutarEnTandas(ids(4), 2, async (tanda): Promise<ResultadoMasivo> => ({
      procesados: tanda.length - 1,
      omitidos: [{ id: tanda[0], motivo: `no aplica a ${tanda[0]}` }],
    }))

    expect(resultado.procesados).toBe(2)
    expect(resultado.omitidos).toEqual([
      { id: 'id-0', motivo: 'no aplica a id-0' },
      { id: 'id-2', motivo: 'no aplica a id-2' },
    ])
  })

  it('does nothing for an empty selection', async () => {
    let llamadas = 0

    const resultado = await ejecutarEnTandas([], 5, async () => {
      llamadas++
      return { procesados: 0, omitidos: [] }
    })

    expect(llamadas).toBe(0)
    expect(resultado).toEqual({ procesados: 0, omitidos: [] })
  })

  it('a selection that fits in one batch is one request', async () => {
    let llamadas = 0

    await ejecutarEnTandas(ids(5), 5, async (tanda) => {
      llamadas++
      return { procesados: tanda.length, omitidos: [] }
    })

    expect(llamadas).toBe(1)
  })

  it('when a batch fails the error goes up and the following batches are not sent', async () => {
    const enviadas: string[][] = []

    await expect(
      ejecutarEnTandas(ids(6), 2, async (tanda) => {
        enviadas.push(tanda)
        if (enviadas.length === 2) throw new Error('el servidor no responde')
        return { procesados: tanda.length, omitidos: [] }
      }),
    ).rejects.toThrow('el servidor no responde')

    expect(enviadas).toHaveLength(2)
  })
})
