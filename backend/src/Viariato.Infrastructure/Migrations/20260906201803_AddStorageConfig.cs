using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "storage_config_id",
                schema: "flujos",
                table: "flujos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "storage_configs",
                schema: "flujos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    proveedor = table.Column<int>(type: "integer", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bucket_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    access_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    secret_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    use_path_style = table.Column<bool>(type: "boolean", nullable: false),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    local_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flujos_storage_config_id",
                schema: "flujos",
                table: "flujos",
                column: "storage_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_configs_nombre",
                schema: "flujos",
                table: "storage_configs",
                column: "nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_flujos_storage_config_storage_config_id",
                schema: "flujos",
                table: "flujos",
                column: "storage_config_id",
                principalSchema: "flujos",
                principalTable: "storage_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_flujos_storage_config_storage_config_id",
                schema: "flujos",
                table: "flujos");

            migrationBuilder.DropTable(
                name: "storage_configs",
                schema: "flujos");

            migrationBuilder.DropIndex(
                name: "ix_flujos_storage_config_id",
                schema: "flujos",
                table: "flujos");

            migrationBuilder.DropColumn(
                name: "storage_config_id",
                schema: "flujos",
                table: "flujos");
        }
    }
}
