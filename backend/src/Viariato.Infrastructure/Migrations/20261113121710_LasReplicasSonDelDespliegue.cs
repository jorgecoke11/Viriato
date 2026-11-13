using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LasReplicasSonDelDespliegue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lanzador_max_copias",
                schema: "rpafleet",
                table: "despliegues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lanzador_max_copias",
                schema: "rpafleet",
                table: "despliegues",
                type: "integer",
                nullable: true);
        }
    }
}
