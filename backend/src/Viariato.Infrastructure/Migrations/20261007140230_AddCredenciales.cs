using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCredenciales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credenciales",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    usuario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    password_cifrado = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    servicio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_acceso_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credenciales", x => x.id);
                    table.ForeignKey(
                        name: "fk_credenciales_servicio_servicio_id",
                        column: x => x.servicio_id,
                        principalSchema: "rpafleet",
                        principalTable: "servicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credenciales_nombre",
                schema: "rpafleet",
                table: "credenciales",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credenciales_servicio_id",
                schema: "rpafleet",
                table: "credenciales",
                column: "servicio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credenciales",
                schema: "rpafleet");
        }
    }
}
