using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Cartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cartera");

            migrationBuilder.CreateTable(
                name: "cotizaciones",
                schema: "cartera",
                columns: table => new
                {
                    ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    precio_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    eur_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotizaciones", x => x.ticker);
                });

            migrationBuilder.CreateTable(
                name: "estado",
                schema: "cartera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capital_inicial_eur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movimientos",
                schema: "cartera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    precio_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    eur_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    importe_eur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    comision_eur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    efectivo_tras_eur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    stop_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimientos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_caso_id",
                schema: "cartera",
                table: "movimientos",
                column: "caso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_fecha",
                schema: "cartera",
                table: "movimientos",
                column: "fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cotizaciones",
                schema: "cartera");

            migrationBuilder.DropTable(
                name: "estado",
                schema: "cartera");

            migrationBuilder.DropTable(
                name: "movimientos",
                schema: "cartera");
        }
    }
}
