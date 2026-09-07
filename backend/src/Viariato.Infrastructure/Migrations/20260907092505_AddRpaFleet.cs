using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRpaFleet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rpafleet");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "despliegue_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipos",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "servicios",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_servicios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "despliegues",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encendido = table.Column<bool>(type: "boolean", nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    api_key_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_despliegues", x => x.id);
                    table.ForeignKey(
                        name: "fk_despliegues_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalSchema: "rpafleet",
                        principalTable: "equipos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_despliegues_servicio_servicio_id",
                        column: x => x.servicio_id,
                        principalSchema: "rpafleet",
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rpa_ejecucion_detalles_despliegue_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                column: "despliegue_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_paso_defs_servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_despliegues_api_key_hash",
                schema: "rpafleet",
                table: "despliegues",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_despliegues_equipo_id_servicio_id_flujo_id",
                schema: "rpafleet",
                table: "despliegues",
                columns: new[] { "equipo_id", "servicio_id", "flujo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_despliegues_servicio_id",
                schema: "rpafleet",
                table: "despliegues",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipos_nombre",
                schema: "rpafleet",
                table: "equipos",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_servicios_nombre",
                schema: "rpafleet",
                table: "servicios",
                column: "nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_flujo_paso_defs_servicio_servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "servicio_id",
                principalSchema: "rpafleet",
                principalTable: "servicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_flujo_paso_defs_servicio_servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs");

            migrationBuilder.DropTable(
                name: "despliegues",
                schema: "rpafleet");

            migrationBuilder.DropTable(
                name: "equipos",
                schema: "rpafleet");

            migrationBuilder.DropTable(
                name: "servicios",
                schema: "rpafleet");

            migrationBuilder.DropIndex(
                name: "ix_rpa_ejecucion_detalles_despliegue_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropIndex(
                name: "ix_flujo_paso_defs_servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropColumn(
                name: "despliegue_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles");

            migrationBuilder.DropColumn(
                name: "servicio_id",
                schema: "flujos",
                table: "flujo_paso_defs");
        }
    }
}
