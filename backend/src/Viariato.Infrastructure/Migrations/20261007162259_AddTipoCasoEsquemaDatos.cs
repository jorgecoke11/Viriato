using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoCasoEsquemaDatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "esquema_datos_json",
                schema: "flujos",
                table: "flujo_tipo_caso_defs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "esquema_datos_json",
                schema: "flujos",
                table: "flujo_tipo_caso_defs");
        }
    }
}
