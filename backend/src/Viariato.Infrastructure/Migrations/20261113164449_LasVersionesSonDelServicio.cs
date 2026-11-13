using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LasVersionesSonDelServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_versiones_de_robot_despliegues_despliegue_id",
                schema: "rpafleet",
                table: "versiones_de_robot");

            // Renumbered per service below: two Despliegues of one service both had a version 1.
            migrationBuilder.DropIndex(
                name: "ix_versiones_de_robot_despliegue_id_numero",
                schema: "rpafleet",
                table: "versiones_de_robot");

            migrationBuilder.RenameColumn(
                name: "despliegue_id",
                schema: "rpafleet",
                table: "versiones_de_robot",
                newName: "servicio_id");

            migrationBuilder.AddColumn<bool>(
                name: "en_el_lanzador",
                schema: "rpafleet",
                table: "despliegues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // What the launcher ran stays what it runs: a Despliegue with a version was in the launcher.
            migrationBuilder.Sql("""
                UPDATE rpafleet.despliegues SET en_el_lanzador = TRUE WHERE version_de_robot_id IS NOT NULL;
                """);

            // Each version now belongs to its Despliegue's service, numbered 1, 2, 3… within it in upload order.
            migrationBuilder.Sql("""
                UPDATE rpafleet.versiones_de_robot v SET servicio_id = d.servicio_id
                FROM rpafleet.despliegues d WHERE d.id = v.servicio_id;

                UPDATE rpafleet.versiones_de_robot v SET numero = n.numero
                FROM (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY servicio_id ORDER BY created_at, id) AS numero
                    FROM rpafleet.versiones_de_robot
                ) n
                WHERE n.id = v.id;
                """);

            // A Despliegue on its service's latest version follows the latest from now on; any other stays held at its own.
            migrationBuilder.Sql("""
                UPDATE rpafleet.despliegues d SET version_de_robot_id = NULL
                WHERE d.version_de_robot_id = (
                    SELECT v.id FROM rpafleet.versiones_de_robot v
                    WHERE v.servicio_id = d.servicio_id ORDER BY v.numero DESC LIMIT 1);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_versiones_de_robot_servicio_id_numero",
                schema: "rpafleet",
                table: "versiones_de_robot",
                columns: new[] { "servicio_id", "numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_versiones_de_robot_servicios_servicio_id",
                schema: "rpafleet",
                table: "versiones_de_robot",
                column: "servicio_id",
                principalSchema: "rpafleet",
                principalTable: "servicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_versiones_de_robot_servicios_servicio_id",
                schema: "rpafleet",
                table: "versiones_de_robot");

            // Back to one Despliegue each: those following the latest are held at it again, every version goes to the oldest
            // Despliegue of its service (one with none is dropped), and the numbers stay unique as they already are per service.
            migrationBuilder.Sql("""
                UPDATE rpafleet.despliegues d SET version_de_robot_id = (
                    SELECT v.id FROM rpafleet.versiones_de_robot v
                    WHERE v.servicio_id = d.servicio_id ORDER BY v.numero DESC LIMIT 1)
                WHERE d.en_el_lanzador AND d.version_de_robot_id IS NULL;

                UPDATE rpafleet.despliegues SET version_de_robot_id = NULL WHERE NOT en_el_lanzador;

                DELETE FROM rpafleet.versiones_de_robot v
                WHERE NOT EXISTS (SELECT 1 FROM rpafleet.despliegues d WHERE d.servicio_id = v.servicio_id);

                UPDATE rpafleet.versiones_de_robot v SET servicio_id = (
                    SELECT d.id FROM rpafleet.despliegues d WHERE d.servicio_id = v.servicio_id ORDER BY d.created_at, d.id LIMIT 1);
                """);

            migrationBuilder.DropColumn(
                name: "en_el_lanzador",
                schema: "rpafleet",
                table: "despliegues");

            migrationBuilder.RenameColumn(
                name: "servicio_id",
                schema: "rpafleet",
                table: "versiones_de_robot",
                newName: "despliegue_id");

            migrationBuilder.RenameIndex(
                name: "ix_versiones_de_robot_servicio_id_numero",
                schema: "rpafleet",
                table: "versiones_de_robot",
                newName: "ix_versiones_de_robot_despliegue_id_numero");

            migrationBuilder.AddForeignKey(
                name: "fk_versiones_de_robot_despliegues_despliegue_id",
                schema: "rpafleet",
                table: "versiones_de_robot",
                column: "despliegue_id",
                principalSchema: "rpafleet",
                principalTable: "despliegues",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
