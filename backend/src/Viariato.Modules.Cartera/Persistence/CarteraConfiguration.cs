using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Infrastructure;
using Viariato.Modules.Cartera.Domain;

namespace Viariato.Modules.Cartera.Persistence;

internal sealed class CarteraModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarteraModuleModelConfiguration).Assembly);
}

public sealed class CarteraEstadoConfiguration : IEntityTypeConfiguration<CarteraEstado>
{
    public void Configure(EntityTypeBuilder<CarteraEstado> builder)
    {
        builder.ToTable("estado", "cartera");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CapitalInicialEur).HasPrecision(18, 2);
    }
}

public sealed class CarteraMovimientoConfiguration : IEntityTypeConfiguration<CarteraMovimiento>
{
    public void Configure(EntityTypeBuilder<CarteraMovimiento> builder)
    {
        builder.ToTable("movimientos", "cartera");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.CasoId).HasMaxLength(64).IsRequired();
        builder.HasIndex(m => m.CasoId).IsUnique();
        builder.Property(m => m.Ticker).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Lado).HasMaxLength(10).IsRequired();
        builder.Property(m => m.Cantidad).HasPrecision(18, 6);
        builder.Property(m => m.PrecioUsd).HasPrecision(18, 6);
        builder.Property(m => m.EurUsd).HasPrecision(18, 6);
        builder.Property(m => m.ImporteEur).HasPrecision(18, 2);
        builder.Property(m => m.ComisionEur).HasPrecision(18, 2);
        builder.Property(m => m.EfectivoTrasEur).HasPrecision(18, 2);
        builder.Property(m => m.StopUsd).HasPrecision(18, 6);
        builder.HasIndex(m => m.Fecha);
    }
}

public sealed class CarteraCotizacionConfiguration : IEntityTypeConfiguration<CarteraCotizacion>
{
    public void Configure(EntityTypeBuilder<CarteraCotizacion> builder)
    {
        builder.ToTable("cotizaciones", "cartera");
        builder.HasKey(c => c.Ticker);
        builder.Property(c => c.Ticker).HasMaxLength(20);
        builder.Property(c => c.PrecioUsd).HasPrecision(18, 6);
        builder.Property(c => c.EurUsd).HasPrecision(18, 6);
    }
}

public sealed class CarteraSenalConfiguration : IEntityTypeConfiguration<CarteraSenal>
{
    public void Configure(EntityTypeBuilder<CarteraSenal> builder)
    {
        builder.ToTable("senales", "cartera");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.CasoId).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.CasoId).IsUnique();
        builder.Property(s => s.Ticker).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Senal).HasMaxLength(10).IsRequired();
        builder.Property(s => s.PrecioUsd).HasPrecision(18, 6);
        builder.Property(s => s.Puntos).HasPrecision(8, 2);
        builder.Property(s => s.VotosJson).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SenalIa).HasMaxLength(10);
        builder.Property(s => s.OrdenLado).HasMaxLength(10);
        builder.Property(s => s.ProbabilidadMl).HasPrecision(6, 4);
        builder.Property(s => s.PrecioEvaluacionUsd).HasPrecision(18, 6);
        builder.Property(s => s.RetornoPorcentaje).HasPrecision(10, 4);
        builder.HasIndex(s => new { s.Ticker, s.OrdenCreadaEn });
        builder.HasIndex(s => new { s.EvaluadaEn, s.Fecha });
    }
}

public sealed class CarteraPesosConfiguration : IEntityTypeConfiguration<CarteraPesos>
{
    public void Configure(EntityTypeBuilder<CarteraPesos> builder)
    {
        builder.ToTable("pesos", "cartera");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PesosJson).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(12).IsRequired();
        builder.Property(p => p.Motivo).HasMaxLength(1000);
        builder.Property(p => p.DetalleJson).HasMaxLength(4000);
        builder.HasIndex(p => p.Version).IsUnique();
    }
}

public sealed class CarteraModeloConfiguration : IEntityTypeConfiguration<CarteraModelo>
{
    public void Configure(EntityTypeBuilder<CarteraModelo> builder)
    {
        builder.ToTable("modelos", "cartera");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ModeloJson).HasMaxLength(8000).IsRequired();
        builder.Property(m => m.Estado).HasMaxLength(12).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(1000);
        builder.HasIndex(m => m.Version).IsUnique();
    }
}
