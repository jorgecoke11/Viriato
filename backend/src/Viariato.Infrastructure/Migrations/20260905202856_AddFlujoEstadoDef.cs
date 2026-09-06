using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlujoEstadoDef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "estado_negocio_actual_id",
                schema: "casos",
                table: "casos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "flujo_estado_defs",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_estado_defs", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujo_estado_defs_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_casos_estado_negocio_actual_id",
                schema: "casos",
                table: "casos",
                column: "estado_negocio_actual_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_estado_defs_flujo_id_codigo",
                schema: "flujos",
                table: "flujo_estado_defs",
                columns: new[] { "flujo_id", "codigo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_casos_flujo_estado_defs_estado_negocio_actual_id",
                schema: "casos",
                table: "casos",
                column: "estado_negocio_actual_id",
                principalSchema: "flujos",
                principalTable: "flujo_estado_defs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // One-time backfill: give every pre-existing Flujo a default set of business estados
            // equivalent to the technical CasoEstado values, then point each pre-existing Caso at the
            // one matching its current CasoEstado. Flujos created after this migration start with zero
            // estados — the user configures their own from here on, this only covers what already
            // existed before estados became configurable.
            migrationBuilder.Sql("""
                INSERT INTO flujos.flujo_estado_defs (id, flujo_id, codigo, display, orden, activo, created_at, updated_at)
                SELECT gen_random_uuid(), f.id, x.codigo, x.display, x.orden, true, now(), now()
                FROM flujos.flujos f
                CROSS JOIN (VALUES
                    ('INICIADO', 'Iniciado', 1),
                    ('EN_PROGRESO', 'En progreso', 2),
                    ('PAUSADO', 'Pausado', 3),
                    ('ESPERANDO_REVISION', 'Esperando revisión', 4),
                    ('COMPLETADO', 'Completado', 5),
                    ('FALLIDO', 'Fallido', 6),
                    ('CANCELADO', 'Cancelado', 7)
                ) AS x(codigo, display, orden);

                UPDATE casos.casos c
                SET estado_negocio_actual_id = fed.id
                FROM flujos.flujo_estado_defs fed
                WHERE fed.flujo_id = c.flujo_id
                  AND fed.codigo = CASE c.estado
                    WHEN 'Iniciado' THEN 'INICIADO'
                    WHEN 'EnProgreso' THEN 'EN_PROGRESO'
                    WHEN 'Pausado' THEN 'PAUSADO'
                    WHEN 'EsperandoRevisionHumana' THEN 'ESPERANDO_REVISION'
                    WHEN 'Completado' THEN 'COMPLETADO'
                    WHEN 'Fallido' THEN 'FALLIDO'
                    WHEN 'Cancelado' THEN 'CANCELADO'
                  END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_casos_flujo_estado_defs_estado_negocio_actual_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropTable(
                name: "flujo_estado_defs",
                schema: "flujos");

            migrationBuilder.DropIndex(
                name: "ix_casos_estado_negocio_actual_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "estado_negocio_actual_id",
                schema: "casos",
                table: "casos");
        }
    }
}
