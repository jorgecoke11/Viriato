using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlujoEstadoDefEsFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_final",
                schema: "flujos",
                table: "flujo_estado_defs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: the three legacy terminal codes seeded by AddFlujoEstadoDef are genuinely
            // end-of-the-line outcomes — mark them as such instead of leaving every pre-existing estado
            // at the new column's default of false.
            migrationBuilder.Sql("""
                UPDATE flujos.flujo_estado_defs
                SET es_final = true
                WHERE codigo IN ('COMPLETADO', 'FALLIDO', 'CANCELADO');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "es_final",
                schema: "flujos",
                table: "flujo_estado_defs");
        }
    }
}
