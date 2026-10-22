using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Casos.Lanzadores;

/// <summary>The title a creator writes for a Caso when nobody chose one — the same for a person, a launcher and a robot.</summary>
public static class TituloDeCreador
{
    /// <summary>Takes the creator's next number and writes the title with it. The number is taken from the database in one statement,
    /// so two cases created at the same moment never share one; a case that is then rejected leaves a gap, which is better than a
    /// repeated number. Expects the creator with its Flujo and TipoCaso loaded.</summary>
    public static async Task<string> SiguienteAsync(AppDbContext db, CreadorDeCaso creador, DateTimeOffset cuando, string? datosJson, CancellationToken ct)
    {
        // `update … returning` cannot be composed over, so the row is read back as a list.
        var n = (await db.Database
            .SqlQuery<long>($"update flujos.creadores_de_caso set secuencia = secuencia + 1 where id = {creador.Id} returning secuencia as \"Value\"")
            .ToListAsync(ct)).Single();
        var titulo = Escribir(creador, n, cuando, datosJson);
        return string.IsNullOrWhiteSpace(titulo) ? creador.Nombre : titulo;
    }

    /// <summary>The title with number <paramref name="n"/>, without taking it: what the next case would be called.</summary>
    public static string Escribir(CreadorDeCaso creador, long n, DateTimeOffset cuando, string? datosJson) =>
        PlantillaDeTitulo.Renderizar(
            creador.PlantillaTitulo,
            new Dictionary<string, string>
            {
                ["creador"] = creador.Nombre,
                ["proceso"] = creador.Flujo?.Nombre ?? string.Empty,
                ["tipo"] = creador.TipoCaso?.Nombre ?? string.Empty,
                ["fecha"] = cuando.ToString("yyyy-MM-dd"),
                ["hora"] = cuando.ToString("HH:mm"),
                ["n"] = n.ToString(),
            },
            datosJson);
}
