using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Familias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "caso_id",
                schema: "notificaciones",
                table: "envios",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "familia_id",
                schema: "notificaciones",
                table: "envios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tipos_de_familia",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creador_al_terminar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    condicion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_de_familia", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipos_de_familia_creadores_de_caso_creador_al_terminar_id",
                        column: x => x.creador_al_terminar_id,
                        principalSchema: "flujos",
                        principalTable: "creadores_de_caso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tipos_de_familia_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "familias",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_de_familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    participantes_esperados = table.Column<int>(type: "integer", nullable: true),
                    completados = table.Column<int>(type: "integer", nullable: true),
                    fallidos = table.Column<int>(type: "integer", nullable: true),
                    cancelados = table.Column<int>(type: "integer", nullable: true),
                    caso_de_cierre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nota_de_cierre = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creada_por_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creada_por_despliegue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cerrada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    terminada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familias", x => x.id);
                    table.ForeignKey(
                        name: "fk_familias_casos_caso_de_cierre_id",
                        column: x => x.caso_de_cierre_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_familias_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_familias_tipo_de_familia_tipo_de_familia_id",
                        column: x => x.tipo_de_familia_id,
                        principalSchema: "casos",
                        principalTable: "tipos_de_familia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "familia_eventos",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    detalle_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familia_eventos", x => x.id);
                    table.ForeignKey(
                        name: "fk_familia_eventos_familias_familia_id",
                        column: x => x.familia_id,
                        principalSchema: "casos",
                        principalTable: "familias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "familia_miembros",
                schema: "casos",
                columns: table => new
                {
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anadido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familia_miembros", x => new { x.familia_id, x.caso_id });
                    table.ForeignKey(
                        name: "fk_familia_miembros_casos_caso_id",
                        column: x => x.caso_id,
                        principalSchema: "casos",
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_familia_miembros_familias_familia_id",
                        column: x => x.familia_id,
                        principalSchema: "casos",
                        principalTable: "familias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_envios_familia_id",
                schema: "notificaciones",
                table: "envios",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_familia_eventos_familia_id_occurred_at",
                schema: "casos",
                table: "familia_eventos",
                columns: new[] { "familia_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_familia_miembros_caso_id",
                schema: "casos",
                table: "familia_miembros",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_familias_caso_de_cierre_id",
                schema: "casos",
                table: "familias",
                column: "caso_de_cierre_id");

            migrationBuilder.CreateIndex(
                name: "ix_familias_estado",
                schema: "casos",
                table: "familias",
                column: "estado",
                filter: "estado <> 'Terminada'");

            migrationBuilder.CreateIndex(
                name: "ix_familias_flujo_id_created_at",
                schema: "casos",
                table: "familias",
                columns: new[] { "flujo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_familias_tipo_de_familia_id",
                schema: "casos",
                table: "familias",
                column: "tipo_de_familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_familia_creador_al_terminar_id",
                schema: "casos",
                table: "tipos_de_familia",
                column: "creador_al_terminar_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_de_familia_flujo_id_nombre",
                schema: "casos",
                table: "tipos_de_familia",
                columns: new[] { "flujo_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_envios_familias_familia_id",
                schema: "notificaciones",
                table: "envios",
                column: "familia_id",
                principalSchema: "casos",
                principalTable: "familias",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_envios_familias_familia_id",
                schema: "notificaciones",
                table: "envios");

            migrationBuilder.DropTable(
                name: "familia_eventos",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "familia_miembros",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "familias",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "tipos_de_familia",
                schema: "casos");

            migrationBuilder.DropIndex(
                name: "ix_envios_familia_id",
                schema: "notificaciones",
                table: "envios");

            migrationBuilder.DropColumn(
                name: "familia_id",
                schema: "notificaciones",
                table: "envios");

            migrationBuilder.AlterColumn<Guid>(
                name: "caso_id",
                schema: "notificaciones",
                table: "envios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
