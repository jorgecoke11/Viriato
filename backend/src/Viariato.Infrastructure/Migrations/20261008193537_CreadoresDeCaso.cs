using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreadoresDeCaso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creadores_de_caso",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tipo_caso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paso_inicial_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estado_negocio_inicial_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plantilla_titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    secuencia = table.Column<long>(type: "bigint", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creadores_de_caso", x => x.id);
                    table.ForeignKey(
                        name: "fk_creadores_de_caso_flujo_estado_def_estado_negocio_inicial_id",
                        column: x => x.estado_negocio_inicial_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_estado_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_creadores_de_caso_flujo_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_creadores_de_caso_flujo_tipo_caso_def_tipo_caso_id",
                        column: x => x.tipo_caso_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_tipo_caso_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creadores_de_caso_estado_negocio_inicial_id",
                schema: "flujos",
                table: "creadores_de_caso",
                column: "estado_negocio_inicial_id");

            migrationBuilder.CreateIndex(
                name: "ix_creadores_de_caso_flujo_id_nombre",
                schema: "flujos",
                table: "creadores_de_caso",
                columns: new[] { "flujo_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creadores_de_caso_tipo_caso_id",
                schema: "flujos",
                table: "creadores_de_caso",
                column: "tipo_caso_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creadores_de_caso",
                schema: "flujos");
        }
    }
}
