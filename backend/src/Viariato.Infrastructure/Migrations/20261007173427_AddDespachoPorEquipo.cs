using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDespachoPorEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "equipos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "politica",
                schema: "rpafleet",
                table: "equipos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Prioridad");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ultima_consulta_cola_at",
                schema: "rpafleet",
                table: "despliegues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipo_servicio_orden",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo_servicio_orden", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipo_servicio_orden_equipos_equipo_id",
                        column: x => x.equipo_id,
                        principalSchema: "rpafleet",
                        principalTable: "equipos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_equipo_servicio_orden_servicio_servicio_id",
                        column: x => x.servicio_id,
                        principalSchema: "rpafleet",
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plantillas_despacho",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    max_ejecuciones_simultaneas = table.Column<int>(type: "integer", nullable: false),
                    politica = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantillas_despacho", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantilla_despacho_servicios",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantilla_despacho_servicios", x => x.id);
                    table.ForeignKey(
                        name: "fk_plantilla_despacho_servicios_plantillas_despacho_plantilla_",
                        column: x => x.plantilla_id,
                        principalSchema: "rpafleet",
                        principalTable: "plantillas_despacho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plantilla_despacho_servicios_servicio_servicio_id",
                        column: x => x.servicio_id,
                        principalSchema: "rpafleet",
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipo_servicio_orden_equipo_id_servicio_id",
                schema: "rpafleet",
                table: "equipo_servicio_orden",
                columns: new[] { "equipo_id", "servicio_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipo_servicio_orden_servicio_id",
                schema: "rpafleet",
                table: "equipo_servicio_orden",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_plantilla_despacho_servicios_plantilla_id_servicio_id",
                schema: "rpafleet",
                table: "plantilla_despacho_servicios",
                columns: new[] { "plantilla_id", "servicio_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plantilla_despacho_servicios_servicio_id",
                schema: "rpafleet",
                table: "plantilla_despacho_servicios",
                column: "servicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_plantillas_despacho_nombre",
                schema: "rpafleet",
                table: "plantillas_despacho",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_servicio_orden",
                schema: "rpafleet");

            migrationBuilder.DropTable(
                name: "plantilla_despacho_servicios",
                schema: "rpafleet");

            migrationBuilder.DropTable(
                name: "plantillas_despacho",
                schema: "rpafleet");

            migrationBuilder.DropColumn(
                name: "max_ejecuciones_simultaneas",
                schema: "rpafleet",
                table: "equipos");

            migrationBuilder.DropColumn(
                name: "politica",
                schema: "rpafleet",
                table: "equipos");

            migrationBuilder.DropColumn(
                name: "ultima_consulta_cola_at",
                schema: "rpafleet",
                table: "despliegues");
        }
    }
}
