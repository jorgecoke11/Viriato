using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParametrosEditablesPorUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "editable_por_usuario",
                schema: "flujos",
                table: "flujo_parametros",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "etiqueta",
                schema: "flujos",
                table: "flujo_parametros",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "editable_por_usuario",
                schema: "flujos",
                table: "flujo_parametros");

            migrationBuilder.DropColumn(
                name: "etiqueta",
                schema: "flujos",
                table: "flujo_parametros");
        }
    }
}
