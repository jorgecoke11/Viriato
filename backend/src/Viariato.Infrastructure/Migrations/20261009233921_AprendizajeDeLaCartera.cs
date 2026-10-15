using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AprendizajeDeLaCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modelos",
                schema: "cartera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    modelo_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    muestras_de_entrenamiento = table.Column<int>(type: "integer", nullable: false),
                    muestras_de_prueba = table.Column<int>(type: "integer", nullable: false),
                    precision = table.Column<double>(type: "double precision", nullable: false),
                    precision_base = table.Column<double>(type: "double precision", nullable: false),
                    senales = table.Column<int>(type: "integer", nullable: false),
                    retorno_medio_con_senal_porcentaje = table.Column<double>(type: "double precision", nullable: false),
                    retorno_medio_general_porcentaje = table.Column<double>(type: "double precision", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pesos",
                schema: "cartera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    pesos_json = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    muestras = table.Column<int>(type: "integer", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    detalle_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resuelta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resuelta_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pesos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "senales",
                schema: "cartera",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    precio_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    senal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    puntos = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    votos_json = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    senal_ia = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    confianza_ia = table.Column<int>(type: "integer", nullable: true),
                    probabilidad_ml = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    orden_lado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    orden_creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evaluada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_evaluacion = table.Column<DateOnly>(type: "date", nullable: true),
                    precio_evaluacion_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    retorno_porcentaje = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    acierto = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_senales", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_modelos_version",
                schema: "cartera",
                table: "modelos",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pesos_version",
                schema: "cartera",
                table: "pesos",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_senales_caso_id",
                schema: "cartera",
                table: "senales",
                column: "caso_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_senales_evaluada_en_fecha",
                schema: "cartera",
                table: "senales",
                columns: new[] { "evaluada_en", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_senales_ticker_orden_creada_en",
                schema: "cartera",
                table: "senales",
                columns: new[] { "ticker", "orden_creada_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modelos",
                schema: "cartera");

            migrationBuilder.DropTable(
                name: "pesos",
                schema: "cartera");

            migrationBuilder.DropTable(
                name: "senales",
                schema: "cartera");
        }
    }
}
