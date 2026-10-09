using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viariato.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CasosPendientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The state is stored by name, so nothing changes in the schema: only the Casos that were already waiting in the
            // queue have to be told apart from the ones a robot is running (until now both said "EnProgreso").
            migrationBuilder.Sql(@"
                UPDATE casos.casos c SET estado = 'Pendiente'
                WHERE c.estado = 'EnProgreso'
                  AND EXISTS (
                      SELECT 1 FROM casos.ejecucion_pasos p
                      JOIN casos.rpa_ejecucion_detalles d ON d.ejecucion_paso_id = p.id
                      WHERE p.caso_id = c.id AND p.estado = 'EnProgreso' AND d.despliegue_id IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE casos.casos SET estado = 'EnProgreso' WHERE estado = 'Pendiente';");
        }
    }
}
