using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FamiliasQueSeFormanSolas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "paso_que_la_forma",
                schema: "casos",
                table: "tipos_de_familia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "una_por_tipo_de_caso",
                schema: "casos",
                table: "tipos_de_familia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "caso_origen_id",
                schema: "casos",
                table: "familias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "paso_origen_id",
                schema: "casos",
                table: "familias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_caso_id",
                schema: "casos",
                table: "familias",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "caso_padre_id",
                schema: "casos",
                table: "casos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_familias_caso_origen_id_tipo_de_familia_id",
                schema: "casos",
                table: "familias",
                columns: new[] { "caso_origen_id", "tipo_de_familia_id" });

            migrationBuilder.CreateIndex(
                name: "ix_familias_tipo_caso_id",
                schema: "casos",
                table: "familias",
                column: "tipo_caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_caso_padre_id",
                schema: "casos",
                table: "casos",
                column: "caso_padre_id");

            migrationBuilder.AddForeignKey(
                name: "fk_casos_casos_caso_padre_id",
                schema: "casos",
                table: "casos",
                column: "caso_padre_id",
                principalSchema: "casos",
                principalTable: "casos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_familias_casos_caso_origen_id",
                schema: "casos",
                table: "familias",
                column: "caso_origen_id",
                principalSchema: "casos",
                principalTable: "casos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_familias_flujo_tipo_caso_defs_tipo_caso_id",
                schema: "casos",
                table: "familias",
                column: "tipo_caso_id",
                principalSchema: "flujos",
                principalTable: "flujo_tipo_caso_defs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_casos_casos_caso_padre_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropForeignKey(
                name: "fk_familias_casos_caso_origen_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropForeignKey(
                name: "fk_familias_flujo_tipo_caso_defs_tipo_caso_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropIndex(
                name: "ix_familias_caso_origen_id_tipo_de_familia_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropIndex(
                name: "ix_familias_tipo_caso_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropIndex(
                name: "ix_casos_caso_padre_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "paso_que_la_forma",
                schema: "casos",
                table: "tipos_de_familia");

            migrationBuilder.DropColumn(
                name: "una_por_tipo_de_caso",
                schema: "casos",
                table: "tipos_de_familia");

            migrationBuilder.DropColumn(
                name: "caso_origen_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropColumn(
                name: "paso_origen_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropColumn(
                name: "tipo_caso_id",
                schema: "casos",
                table: "familias");

            migrationBuilder.DropColumn(
                name: "caso_padre_id",
                schema: "casos",
                table: "casos");
        }
    }
}
