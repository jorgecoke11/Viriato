using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentoClasificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_documento",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_documento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_clasificaciones",
                schema: "casos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pagina_desde = table.Column<int>(type: "integer", nullable: false),
                    pagina_hasta = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documento_clasificaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_documento_clasificaciones_documento_documento_id",
                        column: x => x.documento_id,
                        principalSchema: "casos",
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_documento_clasificaciones_tipo_documento_tipo_documento_id",
                        column: x => x.tipo_documento_id,
                        principalSchema: "casos",
                        principalTable: "tipos_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documento_clasificaciones_documento_id",
                schema: "casos",
                table: "documento_clasificaciones",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_documento_clasificaciones_tipo_documento_id",
                schema: "casos",
                table: "documento_clasificaciones",
                column: "tipo_documento_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_documento_nombre",
                schema: "casos",
                table: "tipos_documento",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documento_clasificaciones",
                schema: "casos");

            migrationBuilder.DropTable(
                name: "tipos_documento",
                schema: "casos");
        }
    }
}
