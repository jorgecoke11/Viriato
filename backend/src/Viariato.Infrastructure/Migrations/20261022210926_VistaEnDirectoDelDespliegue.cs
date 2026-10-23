using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VistaEnDirectoDelDespliegue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grabacion_de_video",
                schema: "rpafleet",
                table: "despliegues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SegunRobot");

            migrationBuilder.AddColumn<string>(
                name: "vista_en_directo",
                schema: "rpafleet",
                table: "despliegues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Baja");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grabacion_de_video",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "vista_en_directo",
                schema: "rpafleet",
                table: "despliegues");
        }
    }
}
