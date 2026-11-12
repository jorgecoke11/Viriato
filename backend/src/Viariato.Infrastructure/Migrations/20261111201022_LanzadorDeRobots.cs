using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LanzadorDeRobots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lanzador_copias",
                schema: "rpafleet",
                table: "despliegues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lanzador_detalle",
                schema: "rpafleet",
                table: "despliegues",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lanzador_estado",
                schema: "rpafleet",
                table: "despliegues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lanzador_max_copias",
                schema: "rpafleet",
                table: "despliegues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lanzador_visto_en",
                schema: "rpafleet",
                table: "despliegues",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lanzador_copias",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "lanzador_detalle",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "lanzador_estado",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "lanzador_max_copias",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "lanzador_visto_en",
                schema: "rpafleet",
                table: "despliegues");
        }
    }
}
