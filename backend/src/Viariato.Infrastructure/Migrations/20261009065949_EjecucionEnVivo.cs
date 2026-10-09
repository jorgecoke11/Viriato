using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EjecucionEnVivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "progreso_at",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "progreso_mensaje",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progreso_porcentaje",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vista_en_directo_url",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "progreso_at",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropColumn(
                name: "progreso_mensaje",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropColumn(
                name: "progreso_porcentaje",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropColumn(
                name: "vista_en_directo_url",
                schema: "casos",
                table: "rpa_ejecucion_detalles");
        }
    }
}
