using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCasosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "casos");

            migrationBuilder.CreateTable(
                name: "agente_ejecucion_detalles",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_paso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    decisiones_json = table.Column<string>(type: "jsonb", nullable: true),
                    herramientas_usadas = table.Column<string>(type: "jsonb", nullable: true),
                    input_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    output_snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agente_ejecucion_detalles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "caso_eventos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detalle_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caso_eventos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "casos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    datos_json = table.Column<string>(type: "jsonb", nullable: true),
                    ejecucion_actual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_casos", x => x.id);
                    table.ForeignKey(
                        name: "fk_casos_flujo_versiones_flujo_version_id",
                        column: x => x.flujo_version_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_casos_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_paso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ejecucion_pasos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_paso_def_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_paso = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    numero_intento = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_mensaje = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ejecucion_pasos", x => x.id);
                    table.ForeignKey(
                        name: "fk_ejecucion_pasos_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ejecucion_pasos_flujo_paso_defs_flujo_paso_def_id",
                        column: x => x.flujo_paso_def_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_paso_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ejecucion_pasos_trabajos_trabajo_id",
                        column: x => x.trabajo_id,
                        principalSchema: "ops",
                        principalTable: "trabajos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ejecuciones",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    paso_actual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ejecuciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_ejecuciones_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ejecuciones_ejecucion_paso_paso_actual_id",
                        column: x => x.paso_actual_id,
                        principalSchema: "casos",
                        principalTable: "ejecucion_pasos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ejecuciones_flujo_versiones_flujo_version_id",
                        column: x => x.flujo_version_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidencias",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_paso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contenido_json = table.Column<string>(type: "jsonb", nullable: true),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidencias", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidencias_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidencias_documentos_documento_id",
                        column: x => x.documento_id,
                        principalSchema: "casos",
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_evidencias_ejecucion_pasos_ejecucion_paso_id",
                        column: x => x.ejecucion_paso_id,
                        principalSchema: "casos",
                        principalTable: "ejecucion_pasos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revisiones_humanas",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_paso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comentario = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisiones_humanas", x => x.id);
                    table.ForeignKey(
                        name: "fk_revisiones_humanas_ejecucion_pasos_ejecucion_paso_id",
                        column: x => x.ejecucion_paso_id,
                        principalSchema: "casos",
                        principalTable: "ejecucion_pasos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rpa_ejecucion_detalles",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_paso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aplicacion_objetivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    worker_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parametros_entrada = table.Column<string>(type: "jsonb", nullable: true),
                    parametros_salida = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rpa_ejecucion_detalles", x => x.id);
                    table.ForeignKey(
                        name: "fk_rpa_ejecucion_detalles_ejecucion_pasos_ejecucion_paso_id",
                        column: x => x.ejecucion_paso_id,
                        principalSchema: "casos",
                        principalTable: "ejecucion_pasos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agente_ejecucion_detalles_ejecucion_paso_id",
                schema: "casos",
                table: "agente_ejecucion_detalles",
                column: "ejecucion_paso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caso_eventos_caso_id",
                schema: "casos",
                table: "caso_eventos",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_caso_eventos_ejecucion_id",
                schema: "casos",
                table: "caso_eventos",
                column: "ejecucion_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_datos_json",
                schema: "casos",
                table: "casos",
                column: "datos_json")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_casos_ejecucion_actual_id",
                schema: "casos",
                table: "casos",
                column: "ejecucion_actual_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_estado",
                schema: "casos",
                table: "casos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_casos_flujo_id",
                schema: "casos",
                table: "casos",
                column: "flujo_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_flujo_version_id",
                schema: "casos",
                table: "casos",
                column: "flujo_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_caso_id",
                schema: "casos",
                table: "documentos",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_documentos_ejecucion_paso_id",
                schema: "casos",
                table: "documentos",
                column: "ejecucion_paso_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_caso_id",
                schema: "casos",
                table: "ejecucion_pasos",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_ejecucion_id_flujo_paso_def_id",
                schema: "casos",
                table: "ejecucion_pasos",
                columns: new[] { "ejecucion_id", "flujo_paso_def_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_ejecucion_id_flujo_paso_def_id_numero_inten",
                schema: "casos",
                table: "ejecucion_pasos",
                columns: new[] { "ejecucion_id", "flujo_paso_def_id", "numero_intento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_estado",
                schema: "casos",
                table: "ejecucion_pasos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_flujo_paso_def_id",
                schema: "casos",
                table: "ejecucion_pasos",
                column: "flujo_paso_def_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecucion_pasos_trabajo_id",
                schema: "casos",
                table: "ejecucion_pasos",
                column: "trabajo_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecuciones_caso_id",
                schema: "casos",
                table: "ejecuciones",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecuciones_estado",
                schema: "casos",
                table: "ejecuciones",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_ejecuciones_flujo_version_id",
                schema: "casos",
                table: "ejecuciones",
                column: "flujo_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejecuciones_paso_actual_id",
                schema: "casos",
                table: "ejecuciones",
                column: "paso_actual_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencias_caso_id",
                schema: "casos",
                table: "evidencias",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencias_documento_id",
                schema: "casos",
                table: "evidencias",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencias_ejecucion_paso_id",
                schema: "casos",
                table: "evidencias",
                column: "ejecucion_paso_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencias_tipo",
                schema: "casos",
                table: "evidencias",
                column: "tipo");

            migrationBuilder.CreateIndex(
                name: "ix_revisiones_humanas_ejecucion_paso_id",
                schema: "casos",
                table: "revisiones_humanas",
                column: "ejecucion_paso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rpa_ejecucion_detalles_ejecucion_paso_id",
                schema: "casos",
                table: "rpa_ejecucion_detalles",
                column: "ejecucion_paso_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_agente_ejecucion_detalles_ejecucion_paso_ejecucion_paso_id",
                schema: "casos",
                table: "agente_ejecucion_detalles",
                column: "ejecucion_paso_id",
                principalSchema: "casos",
                principalTable: "ejecucion_pasos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_caso_eventos_casos_caso_id",
                schema: "casos",
                table: "caso_eventos",
                column: "caso_id",
                principalSchema: "casos",
                principalTable: "casos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_caso_eventos_ejecucion_ejecucion_id",
                schema: "casos",
                table: "caso_eventos",
                column: "ejecucion_id",
                principalSchema: "casos",
                principalTable: "ejecuciones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_casos_ejecucion_ejecucion_actual_id",
                schema: "casos",
                table: "casos",
                column: "ejecucion_actual_id",
                principalSchema: "casos",
                principalTable: "ejecuciones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_documentos_ejecucion_paso_ejecucion_paso_id",
                schema: "casos",
                table: "documentos",
                column: "ejecucion_paso_id",
                principalSchema: "casos",
                principalTable: "ejecucion_pasos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_ejecucion_pasos_ejecuciones_ejecucion_id",
                schema: "casos",
                table: "ejecucion_pasos",
                column: "ejecucion_id",
                principalSchema: "casos",
                principalTable: "ejecuciones",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ejecuciones_ejecucion_paso_paso_actual_id",
                schema: "casos",
                table: "ejecuciones");

            migrationBuilder.DropForeignKey(
                name: "fk_ejecuciones_casos_caso_id",
                schema: "casos",
                table: "ejecuciones");

            migrationBuilder.DropTable(
                name: "agente_ejecucion_detalles",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "caso_eventos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "evidencias",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "revisiones_humanas",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "rpa_ejecucion_detalles",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "documentos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "ejecucion_pasos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "casos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "ejecuciones",
                schema: "casos");
        }
    }
}
