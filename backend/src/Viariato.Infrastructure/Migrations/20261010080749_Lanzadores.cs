using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Lanzadores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lanzadores_de_caso",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creador_de_caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    datos_json = table.Column<string>(type: "jsonb", nullable: true),
                    cron = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    zona_horaria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tolerancia_minutos = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_disparo_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_caso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ultimo_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lanzadores_de_caso", x => x.id);
                    table.ForeignKey(
                        name: "fk_lanzadores_de_caso_creadores_de_caso_creador_de_caso_id",
                        column: x => x.creador_de_caso_id,
                        principalSchema: "flujos",
                        principalTable: "creadores_de_caso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lanzadores_de_caso_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lanzadores_de_caso_creador_de_caso_id",
                schema: "flujos",
                table: "lanzadores_de_caso",
                column: "creador_de_caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_lanzadores_de_caso_flujo_id_nombre",
                schema: "flujos",
                table: "lanzadores_de_caso",
                columns: new[] { "flujo_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lanzadores_de_caso",
                schema: "flujos");
        }
    }
}
