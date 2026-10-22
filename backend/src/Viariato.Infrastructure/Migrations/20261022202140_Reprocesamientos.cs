using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reprocesamientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reglas_de_reprocesamiento",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    estado_negocio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado_ejecucion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    paso_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    espera_minutos = table.Column<int>(type: "integer", nullable: false),
                    limite = table.Column<int>(type: "integer", nullable: true),
                    estado_final_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reglas_de_reprocesamiento", x => x.id);
                    table.ForeignKey(
                        name: "fk_reglas_de_reprocesamiento_flujo_estado_defs_estado_final_id",
                        column: x => x.estado_final_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_estado_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglas_de_reprocesamiento_flujo_estado_defs_estado_negocio_",
                        column: x => x.estado_negocio_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_estado_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglas_de_reprocesamiento_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reprocesamientos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regla_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paso_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    programado_para = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hecho_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reprocesamientos", x => x.id);
                    table.ForeignKey(
                        name: "fk_reprocesamientos_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reprocesamientos_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reprocesamientos_reglas_de_reprocesamiento_regla_id",
                        column: x => x.regla_id,
                        principalSchema: "casos",
                        principalTable: "reglas_de_reprocesamiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reglas_de_reprocesamiento_estado_final_id",
                schema: "casos",
                table: "reglas_de_reprocesamiento",
                column: "estado_final_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglas_de_reprocesamiento_estado_negocio_id",
                schema: "casos",
                table: "reglas_de_reprocesamiento",
                column: "estado_negocio_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglas_de_reprocesamiento_flujo_id_activa",
                schema: "casos",
                table: "reglas_de_reprocesamiento",
                columns: new[] { "flujo_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "ix_reprocesamientos_caso_id_regla_id",
                schema: "casos",
                table: "reprocesamientos",
                columns: new[] { "caso_id", "regla_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reprocesamientos_estado_programado_para",
                schema: "casos",
                table: "reprocesamientos",
                columns: new[] { "estado", "programado_para" });

            migrationBuilder.CreateIndex(
                name: "ix_reprocesamientos_flujo_id_created_at",
                schema: "casos",
                table: "reprocesamientos",
                columns: new[] { "flujo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reprocesamientos_regla_id",
                schema: "casos",
                table: "reprocesamientos",
                column: "regla_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reprocesamientos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "reglas_de_reprocesamiento",
                schema: "casos");
        }
    }
}
