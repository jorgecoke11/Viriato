using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDespliegueFlujoDestino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "flujo_destino_id",
                schema: "rpafleet",
                table: "despliegues",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "flujo_destino_id",
                schema: "rpafleet",
                table: "despliegues");
        }
    }
}
