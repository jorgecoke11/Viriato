using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TiempoMaximoPorServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ultima_consulta_cola_at",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.AddColumn<int>(
                name: "max_ejecuciones_globales",
                schema: "rpafleet",
                table: "servicios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tiempo_maximo_minutos",
                schema: "rpafleet",
                table: "servicios",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_ejecuciones_globales",
                schema: "rpafleet",
                table: "servicios");

            migrationBuilder.DropColumn(
                name: "tiempo_maximo_minutos",
                schema: "rpafleet",
                table: "servicios");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ultima_consulta_cola_at",
                schema: "rpafleet",
                table: "despliegues",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
