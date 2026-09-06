using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlujosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "flujos");

            migrationBuilder.CreateTable(
                name: "agente_definiciones",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    herramientas_permitidas = table.Column<string>(type: "jsonb", nullable: true),
                    parametros_json = table.Column<string>(type: "jsonb", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agente_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flujo_paso_defs",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_paso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    agente_definicion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    configuracion_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_paso_defs", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujo_paso_defs_agente_definiciones_agente_definicion_id",
                        column: x => x.agente_definicion_id,
                        principalSchema: "flujos",
                        principalTable: "agente_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "flujo_versiones",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_version = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_versiones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flujos",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version_activa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujos", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujos_flujo_version_version_activa_id",
                        column: x => x.version_activa_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agente_definiciones_nombre",
                schema: "flujos",
                table: "agente_definiciones",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_paso_defs_agente_definicion_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "agente_definicion_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_paso_defs_flujo_version_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "flujo_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_paso_defs_flujo_version_id_orden",
                schema: "flujos",
                table: "flujo_paso_defs",
                columns: new[] { "flujo_version_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flujo_paso_defs_tipo_paso",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "tipo_paso");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_versiones_estado",
                schema: "flujos",
                table: "flujo_versiones",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_versiones_flujo_id",
                schema: "flujos",
                table: "flujo_versiones",
                column: "flujo_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_versiones_flujo_id_numero_version",
                schema: "flujos",
                table: "flujo_versiones",
                columns: new[] { "flujo_id", "numero_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flujos_nombre",
                schema: "flujos",
                table: "flujos",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "ix_flujos_version_activa_id",
                schema: "flujos",
                table: "flujos",
                column: "version_activa_id");

            migrationBuilder.AddForeignKey(
                name: "fk_flujo_paso_defs_flujo_version_flujo_version_id",
                schema: "flujos",
                table: "flujo_paso_defs",
                column: "flujo_version_id",
                principalSchema: "flujos",
                principalTable: "flujo_versiones",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_flujo_versiones_flujos_flujo_id",
                schema: "flujos",
                table: "flujo_versiones",
                column: "flujo_id",
                principalSchema: "flujos",
                principalTable: "flujos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_flujos_flujo_version_version_activa_id",
                schema: "flujos",
                table: "flujos");

            migrationBuilder.DropTable(
                name: "flujo_paso_defs",
                schema: "flujos");

            migrationBuilder.DropTable(
                name: "agente_definiciones",
                schema: "flujos");

            migrationBuilder.DropTable(
                name: "flujo_versiones",
                schema: "flujos");

            migrationBuilder.DropTable(
                name: "flujos",
                schema: "flujos");
        }
    }
}
