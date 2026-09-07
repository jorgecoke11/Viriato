using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Contracts;

public static class FlujosDtoMapper
{
    public static FlujoDto ToDto(this Flujo flujo) => new(
        flujo.Id,
        flujo.Nombre,
        flujo.Descripcion,
        flujo.VersionActivaId,
        flujo.VersionActiva?.NumeroVersion,
        flujo.Activo,
        flujo.StorageConfigId,
        flujo.StorageConfig?.Nombre,
        flujo.CreatedAt,
        flujo.UpdatedAt);

    public static FlujoVersionDto ToDto(this FlujoVersion version) => new(
        version.Id,
        version.FlujoId,
        version.NumeroVersion,
        version.Estado.ToString(),
        version.Notas,
        version.CreatedAt,
        version.PublishedAt);

    public static FlujoVersionDetailDto ToDetailDto(this FlujoVersion version) => new(
        version.Id,
        version.FlujoId,
        version.NumeroVersion,
        version.Estado.ToString(),
        version.Notas,
        version.CreatedAt,
        version.PublishedAt,
        version.Pasos.OrderBy(p => p.Orden).Select(p => p.ToDto()).ToList());

    public static FlujoPasoDefDto ToDto(this FlujoPasoDef paso) => new(
        paso.Id,
        paso.FlujoVersionId,
        paso.Orden,
        paso.Nombre,
        paso.TipoPaso.ToString(),
        paso.AgenteDefinicionId,
        paso.ConfiguracionJson);

    public static AsignacionFlujoDto ToDto(this AsignacionFlujo asignacion) => new(
        asignacion.Id, asignacion.FlujoId, asignacion.UserId, asignacion.CreatedAt);

    public static FlujoEstadoDefDto ToDto(this FlujoEstadoDef estado) => new(
        estado.Id, estado.FlujoId, estado.Codigo, estado.Display, estado.Orden, estado.EsFinal, estado.Activo,
        estado.CreatedAt, estado.UpdatedAt);

    public static FlujoTipoCasoDefDto ToDto(this FlujoTipoCasoDef tipo) => new(
        tipo.Id, tipo.FlujoId, tipo.Nombre, tipo.Orden, tipo.Activo, tipo.CreatedAt, tipo.UpdatedAt);

    public static StorageConfigDto ToDto(this StorageConfig config) => new(
        config.Id,
        config.Nombre,
        config.Proveedor,
        config.Endpoint,
        config.Region,
        config.BucketName,
        !string.IsNullOrEmpty(config.AccessKey) && !string.IsNullOrEmpty(config.SecretKey),
        config.UsePathStyle,
        config.UseSsl,
        config.LocalPath,
        config.Activo,
        config.CreatedAt,
        config.UpdatedAt);

    public static AgenteDefinicionDto ToDto(this AgenteDefinicion agente) => new(
        agente.Id,
        agente.Nombre,
        agente.Descripcion,
        agente.Modelo,
        agente.SystemPrompt,
        agente.HerramientasPermitidas,
        agente.ParametrosJson,
        agente.Activo,
        agente.CreatedAt,
        agente.UpdatedAt);
}
