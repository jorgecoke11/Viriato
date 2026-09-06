using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlujoTipoCasoDef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tipo_caso_id",
                schema: "casos",
                table: "casos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "flujo_tipo_caso_defs",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_tipo_caso_defs", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujo_tipo_caso_defs_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_casos_tipo_caso_id",
                schema: "casos",
                table: "casos",
                column: "tipo_caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_flujo_tipo_caso_defs_flujo_id_nombre",
                schema: "flujos",
                table: "flujo_tipo_caso_defs",
                columns: new[] { "flujo_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_casos_flujo_tipo_caso_defs_tipo_caso_id",
                schema: "casos",
                table: "casos",
                column: "tipo_caso_id",
                principalSchema: "flujos",
                principalTable: "flujo_tipo_caso_defs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_casos_flujo_tipo_caso_defs_tipo_caso_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropTable(
                name: "flujo_tipo_caso_defs",
                schema: "flujos");

            migrationBuilder.DropIndex(
                name: "ix_casos_tipo_caso_id",
                schema: "casos",
                table: "casos");

            migrationBuilder.DropColumn(
                name: "tipo_caso_id",
                schema: "casos",
                table: "casos");
        }
    }
}
