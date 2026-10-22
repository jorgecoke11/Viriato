using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConexionesNotificacionesYAlmacen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "conexiones");

            migrationBuilder.EnsureSchema(
                name: "notificaciones");

            migrationBuilder.CreateTable(
                name: "conexiones",
                schema: "conexiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    configuracion_json = table.Column<string>(type: "jsonb", nullable: false),
                    secreto_cifrado = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conexiones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "datos_de_proceso",
                schema: "casos",
                columns: table => new
                {
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_datos_de_proceso", x => new { x.flujo_id, x.clave });
                    table.ForeignKey(
                        name: "fk_datos_de_proceso_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reglas",
                schema: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_caso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    disparador = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    conexion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatarios = table.Column<List<string>>(type: "text[]", nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cuerpo = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reglas", x => x.id);
                    table.ForeignKey(
                        name: "fk_reglas_conexiones_conexion_id",
                        column: x => x.conexion_id,
                        principalSchema: "conexiones",
                        principalTable: "conexiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglas_flujo_tipo_caso_defs_tipo_caso_id",
                        column: x => x.tipo_caso_id,
                        principalSchema: "flujos",
                        principalTable: "flujo_tipo_caso_defs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reglas_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "envios",
                schema: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regla_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conexion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatarios = table.Column<List<string>>(type: "text[]", nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    ultimo_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    proximo_intento_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_envios", x => x.id);
                    table.ForeignKey(
                        name: "fk_envios_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_envios_conexiones_conexion_id",
                        column: x => x.conexion_id,
                        principalSchema: "conexiones",
                        principalTable: "conexiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_envios_regla_de_notificacion_regla_id",
                        column: x => x.regla_id,
                        principalSchema: "notificaciones",
                        principalTable: "reglas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conexiones_nombre",
                schema: "conexiones",
                table: "conexiones",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_envios_caso_id",
                schema: "notificaciones",
                table: "envios",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_envios_conexion_id",
                schema: "notificaciones",
                table: "envios",
                column: "conexion_id");

            migrationBuilder.CreateIndex(
                name: "ix_envios_estado_proximo_intento_at",
                schema: "notificaciones",
                table: "envios",
                columns: new[] { "estado", "proximo_intento_at" });

            migrationBuilder.CreateIndex(
                name: "ix_envios_flujo_id_created_at",
                schema: "notificaciones",
                table: "envios",
                columns: new[] { "flujo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_envios_regla_id",
                schema: "notificaciones",
                table: "envios",
                column: "regla_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglas_conexion_id",
                schema: "notificaciones",
                table: "reglas",
                column: "conexion_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglas_flujo_id_disparador_activa",
                schema: "notificaciones",
                table: "reglas",
                columns: new[] { "flujo_id", "disparador", "activa" });

            migrationBuilder.CreateIndex(
                name: "ix_reglas_tipo_caso_id",
                schema: "notificaciones",
                table: "reglas",
                column: "tipo_caso_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datos_de_proceso",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "envios",
                schema: "notificaciones");

            migrationBuilder.DropTable(
                name: "reglas",
                schema: "notificaciones");

            migrationBuilder.DropTable(
                name: "conexiones",
                schema: "conexiones");
        }
    }
}
