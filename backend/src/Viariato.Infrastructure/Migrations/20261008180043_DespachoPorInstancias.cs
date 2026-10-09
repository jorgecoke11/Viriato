using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DespachoPorInstancias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "instancia_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "plantillas_despacho",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "equipos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.CreateTable(
                name: "despliegue_instancias",
                schema: "rpafleet",
                columns: table => new
                {
                    despliegue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instancia_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_despliegue_instancias", x => new { x.despliegue_id, x.instancia_id });
                    table.ForeignKey(
                        name: "fk_despliegue_instancias_despliegues_despliegue_id",
                        column: x => x.despliegue_id,
                        principalSchema: "rpafleet",
                        principalTable: "despliegues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_despliegue_instancias_last_seen_at",
                schema: "rpafleet",
                table: "despliegue_instancias",
                column: "last_seen_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "despliegue_instancias",
                schema: "rpafleet");

            migrationBuilder.DropColumn(
                name: "instancia_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.AlterColumn<int>(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "plantillas_despacho",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "equipos",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
