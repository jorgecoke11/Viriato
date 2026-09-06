using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateLegacyFlujoEstadoDefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The AddFlujoEstadoDef migration backfilled 7 estados per pre-existing Flujo so old Casos
            // had something sensible to point at — never something a user chose from the Estados tab.
            // Retire them by default so they stop being offered as if they were real business estados;
            // Casos that already point at one keep the FK (untouched history), they just won't be
            // reported as a distinct group in the dashboard anymore (see GetResumenAsync).
            migrationBuilder.Sql("""
                UPDATE flujos.flujo_estado_defs
                SET activo = false, updated_at = now()
                WHERE codigo IN ('INICIADO', 'EN_PROGRESO', 'PAUSADO', 'ESPERANDO_REVISION', 'COMPLETADO', 'FALLIDO', 'CANCELADO');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE flujos.flujo_estado_defs
                SET activo = true, updated_at = now()
                WHERE codigo IN ('INICIADO', 'EN_PROGRESO', 'PAUSADO', 'ESPERANDO_REVISION', 'COMPLETADO', 'FALLIDO', 'CANCELADO');
                """);
        }
    }
}
