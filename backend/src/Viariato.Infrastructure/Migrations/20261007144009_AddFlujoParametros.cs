using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlujoParametros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flujo_parametros",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flujo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flujo_parametros", x => x.id);
                    table.ForeignKey(
                        name: "fk_flujo_parametros_flujos_flujo_id",
                        column: x => x.flujo_id,
                        principalSchema: "flujos",
                        principalTable: "flujos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flujo_parametros_flujo_id_codigo",
                schema: "flujos",
                table: "flujo_parametros",
                columns: new[] { "flujo_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flujo_parametros",
                schema: "flujos");
        }
    }
}
