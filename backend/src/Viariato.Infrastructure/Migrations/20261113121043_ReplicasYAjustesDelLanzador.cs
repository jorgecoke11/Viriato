using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplicasYAjustesDelLanzador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_copias",
                schema: "rpafleet",
                table: "versiones_de_robot");

            migrationBuilder.AddColumn<int>(
                name: "memoria_maxima_mb",
                schema: "rpafleet",
                table: "despliegues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "replicas",
                schema: "rpafleet",
                table: "despliegues",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ajustes_del_lanzador",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_robots_a_la_vez = table.Column<int>(type: "integer", nullable: false),
                    max_con_navegador = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajustes_del_lanzador", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ajustes_del_lanzador",
                schema: "rpafleet");

            migrationBuilder.DropColumn(
                name: "memoria_maxima_mb",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "replicas",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.AddColumn<int>(
                name: "max_copias",
                schema: "rpafleet",
                table: "versiones_de_robot",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
