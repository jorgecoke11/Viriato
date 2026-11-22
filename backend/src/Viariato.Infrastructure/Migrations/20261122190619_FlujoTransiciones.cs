using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlujoTransiciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "maximo_de_visitas",
                schema: "flujos",
                table: "flujo_paso_defs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "posicion_x",
                schema: "flujos",
                table: "flujo_paso_defs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "posicion_y",
                schema: "flujos",
                table: "flujo_paso_defs",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "flujo_transicion_defs",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paso_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_negocio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paso_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_transicion_defs", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujo_transicion_defs_flujo_estado_defs_estado_negocio_id",
                        column: x => x.estado_negocio_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_estado_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_flujo_transicion_defs_flujo_paso_defs_paso_destino_id",
                        column: x => x.paso_destino_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_paso_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_flujo_transicion_defs_flujo_paso_defs_paso_origen_id",
                        column: x => x.paso_origen_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_paso_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_flujo_transicion_defs_flujo_version_flujo_version_id",
                        column: x => x.flujo_version_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flujo_transicion_defs_estado_negocio_id",
                schema: "flujos",
                table: "flujo_transicion_defs",
                column: "estado_negocio_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_transicion_defs_flujo_version_id_paso_origen_id_resul",
                schema: "flujos",
                table: "flujo_transicion_defs",
                columns: new[] { "flujo_version_id", "paso_origen_id", "resultado", "estado_negocio_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_flujo_transicion_defs_paso_destino_id",
                schema: "flujos",
                table: "flujo_transicion_defs",
                column: "paso_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_transicion_defs_paso_origen_id",
                schema: "flujos",
                table: "flujo_transicion_defs",
                column: "paso_origen_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flujo_transicion_defs",
                schema: "flujos");

            migrationBuilder.DropColumn(
                name: "maximo_de_visitas",
                schema: "flujos",
                table: "flujo_paso_defs");

            migrationBuilder.DropColumn(
                name: "posicion_x",
                schema: "flujos",
                table: "flujo_paso_defs");

            migrationBuilder.DropColumn(
                name: "posicion_y",
                schema: "flujos",
                table: "flujo_paso_defs");
        }
    }
}
