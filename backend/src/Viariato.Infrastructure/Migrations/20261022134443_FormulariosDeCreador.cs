using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FormulariosDeCreador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "datos_por_defecto_json",
                schema: "flujos",
                table: "creadores_de_caso",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "formulario_id",
                schema: "flujos",
                table: "creadores_de_caso",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "formulario_id",
                schema: "casos",
                table: "casos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "formularios",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    esquema_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formularios", x => x.id);
                    table.ForeignKey(
                        name: "fk_formularios_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creadores_de_caso_formulario_id",
                schema: "flujos",
                table: "creadores_de_caso",
                column: "formulario_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_formulario_id",
                schema: "casos",
                table: "casos",
                column: "formulario_id");

            migrationBuilder.CreateIndex(
                name: "ix_formularios_flujo_id_nombre",
                schema: "flujos",
                table: "formularios",
                columns: new[] { "flujo_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_casos_formularios_formulario_id",
                schema: "casos",
                table: "casos",
                column: "formulario_id",
                principalSchema: "flujos",
                principalTable: "formularios",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_creadores_de_caso_formulario_formulario_id",
                schema: "flujos",
                table: "creadores_de_caso",
                column: "formulario_id",
                principalSchema: "flujos",
                principalTable: "formularios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The forms used to live in the case types. Each type that had one becomes a form of the same process, with the type's
            // name and — so this can be undone exactly — the type's id; the creators of that type and its Casos point at it. Nothing
            // is lost: the schema moves, it is not rewritten.
            migrationBuilder.Sql("""
                insert into flujos.formularios (id, flujo_id, nombre, descripcion, esquema_json, created_at, updated_at)
                select t.id, t.flujo_id, t.nombre, null, t.esquema_datos_json, t.created_at, t.updated_at
                from flujos.flujo_tipo_caso_defs t
                where t.esquema_datos_json is not null and btrim(t.esquema_datos_json) <> '';

                update flujos.creadores_de_caso c set formulario_id = c.tipo_caso_id
                where exists (select 1 from flujos.formularios f where f.id = c.tipo_caso_id);

                update casos.casos c set formulario_id = c.tipo_caso_id
                where exists (select 1 from flujos.formularios f where f.id = c.tipo_caso_id);
                """);

            migrationBuilder.DropColumn(
                name: "esquema_datos_json",
                schema: "flujos",
                table: "flujo_tipo_caso_defs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "esquema_datos_json",
                schema: "flujos",
                table: "flujo_tipo_caso_defs",
                type: "text",
                nullable: true);

            // Back to the types: a form that came from a type (same id) returns to it; one made afterwards goes to the type of the
            // first creator that uses it, if that type has none yet. Forms no type can take are lost with the table.
            migrationBuilder.Sql("""
                update flujos.flujo_tipo_caso_defs t set esquema_datos_json = f.esquema_json
                from flujos.formularios f where f.id = t.id;

                update flujos.flujo_tipo_caso_defs t set esquema_datos_json = x.esquema_json
                from (
                    select distinct on (c.tipo_caso_id) c.tipo_caso_id, f.esquema_json
                    from flujos.creadores_de_caso c join flujos.formularios f on f.id = c.formulario_id
                    where c.tipo_caso_id is not null
                    order by c.tipo_caso_id, c.orden
                ) x
                where x.tipo_caso_id = t.id and t.esquema_datos_json is null;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_casos_formularios_formulario_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropForeignKey(
                name: "fk_creadores_de_caso_formulario_formulario_id",
                schema: "flujos",
                table: "creadores_de_caso");

            migrationBuilder.DropTable(
                name: "formularios",
                schema: "flujos");

            migrationBuilder.DropIndex(
                name: "ix_creadores_de_caso_formulario_id",
                schema: "flujos",
                table: "creadores_de_caso");

            migrationBuilder.DropIndex(
                name: "ix_casos_formulario_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "datos_por_defecto_json",
                schema: "flujos",
                table: "creadores_de_caso");

            migrationBuilder.DropColumn(
                name: "formulario_id",
                schema: "flujos",
                table: "creadores_de_caso");

            migrationBuilder.DropColumn(
                name: "formulario_id",
                schema: "casos",
                table: "casos");
        }
    }
}
