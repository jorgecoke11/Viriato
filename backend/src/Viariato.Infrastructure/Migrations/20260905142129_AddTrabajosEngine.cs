using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrabajosEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "trabajos",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parent_trabajo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    progress = table.Column<int>(type: "integer", nullable: true),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajos", x => x.id);
                    table.ForeignKey(
                        name: "fk_trabajos_trabajos_parent_trabajo_id",
                        column: x => x.parent_trabajo_id,
                        principalSchema: "ops",
                        principalTable: "trabajos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trabajo_logs",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajo_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_trabajo_logs_trabajos_trabajo_id",
                        column: x => x.trabajo_id,
                        principalSchema: "ops",
                        principalTable: "trabajos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trabajo_logs_trabajo_id_timestamp",
                schema: "ops",
                table: "trabajo_logs",
                columns: new[] { "trabajo_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_data",
                schema: "ops",
                table: "trabajos",
                column: "data")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_parent_trabajo_id",
                schema: "ops",
                table: "trabajos",
                column: "parent_trabajo_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_process_code",
                schema: "ops",
                table: "trabajos",
                column: "process_code");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_status",
                schema: "ops",
                table: "trabajos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_trabajos_subject_type_subject_key",
                schema: "ops",
                table: "trabajos",
                columns: new[] { "subject_type", "subject_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabajo_logs",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "trabajos",
                schema: "ops");
        }
    }
}
