using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VersionesDeRobot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "versiones_de_robot",
                schema: "rpafleet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    despliegue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ruta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    raiz = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    comando = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    argumentos = table.Column<List<string>>(type: "text[]", nullable: false),
                    pantalla = table.Column<bool>(type: "boolean", nullable: false),
                    memoria_maxima_mb = table.Column<int>(type: "integer", nullable: true),
                    max_copias = table.Column<int>(type: "integer", nullable: false),
                    entorno_json = table.Column<string>(type: "jsonb", nullable: true),
                    subida_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versiones_de_robot", x => x.id);
                    table.ForeignKey(
                        name: "fk_versiones_de_robot_despliegues_despliegue_id",
                        column: x => x.despliegue_id,
                        principalSchema: "rpafleet",
                        principalTable: "despliegues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_despliegues_version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues",
                column: "version_de_robot_id");

            migrationBuilder.CreateIndex(
                name: "ix_versiones_de_robot_despliegue_id_numero",
                schema: "rpafleet",
                table: "versiones_de_robot",
                columns: new[] { "despliegue_id", "numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_despliegues_version_de_robot_version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues",
                column: "version_de_robot_id",
                principalSchema: "rpafleet",
                principalTable: "versiones_de_robot",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_despliegues_version_de_robot_version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropTable(
                name: "versiones_de_robot",
                schema: "rpafleet");

            migrationBuilder.DropIndex(
                name: "ix_despliegues_version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.DropColumn(
                name: "version_de_robot_id",
                schema: "rpafleet",
                table: "despliegues");
        }
    }
}
